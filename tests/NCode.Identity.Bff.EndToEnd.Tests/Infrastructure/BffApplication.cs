// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net.Security;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using NCode.Buffers;
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Server;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Hosts a backend-for-frontend (BFF) application on Kestrel over HTTPS, wired to an OpenID Provider, so that
/// real browsers can drive it.
/// </summary>
public sealed class BffApplication : IAsyncDisposable
{
    /// <summary>
    /// The name of the one frontend that the BFF serves.
    /// </summary>
    public const string FrontendName = "web";

    /// <summary>
    /// The name of the session cookie of the frontend.
    /// </summary>
    public const string SessionCookieName = $"{BffDefaults.SessionCookieName}.{FrontendName}";

    /// <summary>
    /// The name of the login cookie of the frontend.
    /// </summary>
    public const string LoginCookieName = $"{BffDefaults.LoginCookieName}.{FrontendName}";

    private const string ConfigurationPrefix = $"Bff:Frontends:{FrontendName}";
    private const string ClientKeyRing = "e2e-client";
    private const string DPoPKeyRing = "e2e-dpop";

    private readonly WebApplication _app;
    private readonly List<ECDsa> _keys = [];
    private int _apiCalls;
    private int _preflightRequests;

    private BffApplication(BffApplicationSettings settings)
    {
        Origin = $"https://localhost:{settings.Port}";

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
            }
        );
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.ListenLocalhost(
                settings.Port,
                listen => listen.UseHttps(settings.Certificate.Certificate)
            )
        );

        var configuration = new Dictionary<string, string?>
        {
            [$"{ConfigurationPrefix}:Authority"] = settings.Authority,
            [$"{ConfigurationPrefix}:PublicOrigin"] = Origin,
            [$"{ConfigurationPrefix}:ClientId"] = settings.ClientId,
            [$"{ConfigurationPrefix}:ClientAuthentication:Method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .PrivateKeyJwt,
            [$"{ConfigurationPrefix}:ClientAuthentication:KeyRing"] = ClientKeyRing,
            [$"{ConfigurationPrefix}:DPoP:KeyRing"] = DPoPKeyRing,
            [$"{ConfigurationPrefix}:Jwks:Enabled"] = "true",
            [$"{ConfigurationPrefix}:IdToken:SigningAlgorithms:0"] = AlgorithmCodes
                .DigitalSignature
                .RsaSsaPssSha256,
            [$"{ConfigurationPrefix}:ReturnUrls:0"] = "/",
            [$"{ConfigurationPrefix}:UserClaims:0"] = "email",
            [$"{ConfigurationPrefix}:Session:SameSite"] = settings.SameSite.ToString(),
        };
        for (var i = 0; i < settings.TrustedOrigins.Count; i++)
        {
            configuration[$"{ConfigurationPrefix}:TrustedOrigins:{i}"] = settings.TrustedOrigins[i];
        }

        builder.Configuration.AddInMemoryCollection(configuration);

        var serviceCollection = builder.Services;
        AddKeyRing(serviceCollection, ClientKeyRing);
        AddKeyRing(serviceCollection, DPoPKeyRing);

        serviceCollection.AddIdentityBff(builder.Configuration);
        serviceCollection.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy
                    .WithOrigins([.. settings.CorsOrigins])
                    .AllowCredentials()
                    .WithHeaders(BffDefaults.AntiforgeryHeaderName)
                    .WithMethods("GET")
            )
        );

        // Registered after the library, which configures its own primary handler for the named client.
        serviceCollection
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(
                (handler, _) =>
                    ((SocketsHttpHandler)handler).SslOptions.RemoteCertificateValidationCallback = (
                        _,
                        certificate,
                        _,
                        errors
                    ) =>
                        errors == SslPolicyErrors.None
                        || settings.Certificate.IsTrusted(certificate)
            );

        var app = builder.Build();
        app.Use(
            async (context, next) =>
            {
                if (
                    HttpMethods.IsOptions(context.Request.Method)
                    && context.Request.Headers.ContainsKey(HeaderNames.AccessControlRequestMethod)
                )
                {
                    Interlocked.Increment(ref _preflightRequests);
                }

                await next(context);
            }
        );
        app.UseRouting();
        app.UseCors();
        app.UseBff();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBffEndpoints();

        app.MapGet(
            "/",
            static () =>
                Results.Content(TestPages.Blank, "text/html", contentEncoding: SecureEncoding.UTF8)
        );
        app.MapGet(
            "/probe/cookie",
            static (HttpContext context) =>
                Results.Text(
                    context.Request.Cookies.ContainsKey(SessionCookieName) ? "present" : "absent",
                    contentEncoding: SecureEncoding.UTF8
                )
        );
        app.MapMethods(
                "/api/me",
                ["GET", "POST"],
                (HttpContext context) =>
                {
                    Interlocked.Increment(ref _apiCalls);
                    return Results.Json(
                        new { sub = context.User.FindFirst(JoseClaimNames.Payload.Sub)?.Value }
                    );
                }
            )
            .AsBffApi();
        app.MapGet(
                "/api/token",
                static async (HttpContext context) =>
                {
                    var accessor = context.RequestServices.GetRequiredService<IBffTokenAccessor>();
                    var request = new BffTokenRequest(BffTokenRequirementNames.User);
                    var result = await accessor.GetAccessTokenAsync(
                        context,
                        request,
                        context.RequestAborted
                    );
                    if (result.Token is not { } token)
                        return Results.StatusCode(result.StatusCode);

                    var fingerprint = Base64Url.EncodeToString(
                        SHA256.HashData(SecureEncoding.UTF8.GetBytes(token.Value))
                    );
                    return Results.Json(new { tokenType = token.TokenType, fingerprint });
                }
            )
            .AsBffApi();

        _app = app;
    }

    /// <summary>
    /// Gets the origin of the BFF, which is <c>https://localhost:{Port}</c>.
    /// </summary>
    public string Origin { get; }

    /// <summary>
    /// Gets the number of requests that reached <c>/api/me</c>.
    /// </summary>
    public int ApiCalls => Volatile.Read(ref _apiCalls);

    /// <summary>
    /// Gets the number of CORS preflight requests that the BFF received.
    /// </summary>
    public int PreflightRequests => Volatile.Read(ref _preflightRequests);

    /// <summary>
    /// Builds and starts a BFF application.
    /// </summary>
    public static async Task<BffApplication> StartAsync(BffApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var application = new BffApplication(settings);
        try
        {
            await application._app.StartAsync();
            return application;
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    private void AddKeyRing(IServiceCollection serviceCollection, string keyRing)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _keys.Add(key);
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            keyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        serviceProvider
                            .GetRequiredService<ISecretKeyFactory>()
                            .CreateEcc(new KeyMetadata { KeyId = keyRing }, key),
                    ])
        );
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        foreach (var key in _keys)
        {
            key.Dispose();
        }
    }
}
