// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
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
    public const string FrontendName = BffHostSetup.FrontendName;

    /// <summary>
    /// The name of the session cookie of the frontend.
    /// </summary>
    public const string SessionCookieName = $"{BffDefaults.SessionCookieName}.{FrontendName}";

    /// <summary>
    /// The name of the login cookie of the frontend.
    /// </summary>
    public const string LoginCookieName = $"{BffDefaults.LoginCookieName}.{FrontendName}";

    private WebApplication App { get; }
    private List<ECDsa> Keys { get; } = [];
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

        builder.Configuration.AddInMemoryCollection(
            BffHostSetup.CreateConfiguration(settings, Origin)
        );

        var serviceCollection = builder.Services;
        serviceCollection.AddIdentityBff(builder.Configuration);
        BffHostSetup.AddServices(serviceCollection, settings, Keys);
        serviceCollection.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy
                    .WithOrigins([.. settings.CorsOrigins])
                    .AllowCredentials()
                    .WithHeaders(BffDefaults.AntiforgeryHeaderName)
                    .WithMethods("GET")
            )
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

        App = app;
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
    public static async ValueTask<BffApplication> StartAsync(
        BffApplicationSettings settings,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

        var application = new BffApplication(settings);
        try
        {
            await application.App.StartAsync(cancellationToken);
            return application;
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        foreach (var key in Keys)
        {
            key.Dispose();
        }
    }
}
