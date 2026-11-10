// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff;
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Server;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Hosts a minimal backend-for-frontend (BFF) application in-process, wired to the NCode OpenID provider of a
/// <see cref="HostApplicationFactory"/>, and drives it as a browser with a cookie jar.
/// </summary>
internal sealed class BffTestHost : IAsyncDisposable
{
    public const string FrontendName = "web";
    public const string ConfigurationPrefix = "Bff:Frontends:web";
    public const string ClientId = "it-bff-e2e";
    public const string ClientSecret = "bff-e2e-Wq4mZ8vK2nT7xR5bL9cY3hF6jD1sGp0E";
    public const string KeyRing = "bff-e2e";
    public const string ApiTokenPath = "/api/token";

    private static Uri BaseAddress { get; } = new("https://bff.example/");

    private WebApplication App { get; }
    private HttpClient Client { get; }
    private Dictionary<string, string> Cookies { get; } = new(StringComparer.Ordinal);

    private BffTestHost(WebApplication app, HttpClient client)
    {
        App = app;
        Client = client;
    }

    /// <summary>
    /// Gets the callback URI that the BFF registers at the provider.
    /// </summary>
    public static string CallbackUri { get; } = new Uri(BaseAddress, "bff/callback").AbsoluteUri;

    /// <summary>
    /// Creates the shared-state instances that several hosts can share.
    /// </summary>
    public static (
        IExpiringKeySet KeySet,
        IExpiringValueSet ValueSet,
        IExpiringIndexSet IndexSet
    ) CreateSharedState()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSharedStateServices()
            .AddInProcessExpiringKeySet()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        var provider = serviceCollection.BuildServiceProvider();
        return (
            provider.GetRequiredService<IExpiringKeySet>(),
            provider.GetRequiredService<IExpiringValueSet>(),
            provider.GetRequiredService<IExpiringIndexSet>()
        );
    }

    /// <summary>
    /// Builds and starts a BFF host.
    /// </summary>
    public static async ValueTask<BffTestHost> StartAsync(
        HostApplicationFactory factory,
        IExpiringKeySet keySet,
        IExpiringValueSet valueSet,
        IExpiringIndexSet indexSet,
        BffTestHostOptions options,
        CancellationToken cancellationToken
    )
    {
        var extraConfiguration = options.ExtraConfiguration;
        var exchanges = options.Exchanges;
        var authenticateBeforeBff = options.AuthenticateBeforeBff;
        var reloadableConfiguration = options.ReloadableConfiguration;
        var configureServices = options.ConfigureServices;

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
            }
        );
        // The host reads only the in-memory settings the test gives it, never the copied host settings files.
        builder.Configuration.Sources.Clear();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{ConfigurationPrefix}:Authority"] = HostApplicationFactory.TestServerIssuer,
                [$"{ConfigurationPrefix}:PublicOrigin"] = "https://bff.example",
                [$"{ConfigurationPrefix}:ClientId"] = ClientId,
                [$"{ConfigurationPrefix}:ClientAuthentication:Method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                [$"{ConfigurationPrefix}:ClientAuthentication:KeyRing"] = KeyRing,
                [$"{ConfigurationPrefix}:ReturnUrls:0"] = "/",
                // The NCode OP issues a refresh token only when offline_access is granted.
                [$"{ConfigurationPrefix}:Scopes:0"] = OpenIdProtocol.ScopeTypes.OfflineAccess,
                [$"{ConfigurationPrefix}:LoginParameters:0"] = OpenIdProtocol.Parameters.Prompt,
                [$"{ConfigurationPrefix}:UserClaims:0"] = "email",
            }
        );

        if (extraConfiguration is not null)
            builder.Configuration.AddInMemoryCollection(extraConfiguration);

        if (reloadableConfiguration is not null)
            ((IConfigurationBuilder)builder.Configuration).Add(reloadableConfiguration);

        var serviceCollection = builder.Services;
        serviceCollection.AddSingleton(keySet);
        serviceCollection.AddSingleton(valueSet);
        serviceCollection.AddSingleton(indexSet);
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            KeyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        serviceProvider
                            .GetRequiredService<ISecretKeyFactory>()
                            .CreateSymmetric(
                                new KeyMetadata { KeyId = "s" },
                                ClientSecret.AsSpan()
                            ),
                    ])
        );

        configureServices?.Invoke(serviceCollection);

        // The replicas share the state registered above; the turnkey composes everything else.
        serviceCollection.AddIdentityBff(builder.Configuration);

        // Registered after the library, which configures its own primary handler for the named client.
        var httpClientBuilder = serviceCollection
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        if (exchanges is not null)
            httpClientBuilder.AddHttpMessageHandler(() =>
                new RecordingHttpMessageHandler(exchanges)
            );

        var app = builder.Build();
        // A misordered pipeline, which UseBff() must refuse at request time.
        if (authenticateBeforeBff)
            app.UseAuthentication();
        app.UseBff();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBffEndpoints();
        app.MapGet(
                ApiTokenPath,
                static async (HttpContext context) =>
                {
                    var accessor = context.RequestServices.GetRequiredService<IBffTokenAccessor>();
                    var request = new BffTokenRequest(BffTokenRequirementNames.User);
                    var result = await accessor.GetAccessTokenAsync(
                        context,
                        request,
                        context.RequestAborted
                    );
                    context.Response.StatusCode = result.StatusCode;
                    var tokenType = result.Token?.TokenType ?? result.Reason ?? string.Empty;
                    await context.Response.WriteAsync(tokenType, context.RequestAborted);
                }
            )
            .AsBffApi();

        await app.StartAsync(cancellationToken);
        var server = app.GetTestServer();
        server.BaseAddress = BaseAddress;
        var client = server.CreateClient();
        return new BffTestHost(app, client);
    }

    /// <summary>
    /// Sends a request with the cookies of the jar and stores the cookies that the response sets.
    /// </summary>
    public ValueTask<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string pathAndQuery,
        IReadOnlyList<(string Name, string Value)> headers,
        CancellationToken cancellationToken
    ) => SendAsync(method, pathAndQuery, content: null, headers, cancellationToken);

    /// <summary>
    /// Sends a request with a body and the cookies of the jar, as a browser that posts a form, and stores the cookies
    /// that the response sets.
    /// </summary>
    public async ValueTask<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string pathAndQuery,
        HttpContent? content,
        IReadOnlyList<(string Name, string Value)> headers,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(method, pathAndQuery) { Content = content };
        if (Cookies.Count > 0)
        {
            var cookie = string.Join("; ", Cookies.Select(pair => $"{pair.Key}={pair.Value}"));
            request.Headers.Add("Cookie", cookie);
        }

        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        var response = await Client.SendAsync(request, cancellationToken);
        StoreCookies(response);
        return response;
    }

    /// <summary>
    /// Removes every cookie from the jar.
    /// </summary>
    public void ClearCookies() => Cookies.Clear();

    /// <summary>
    /// Posts a form without the cookies of the jar, as a server-to-server call from the provider.
    /// </summary>
    public async ValueTask<HttpResponseMessage> PostFormAsync(
        string path,
        HttpContent content,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = content;
        return await Client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Replaces the cookies of the jar with a copy of the cookies of another host's jar, as a browser that sends its
    /// cookie to a different replica of the same application.
    /// </summary>
    public void CopyCookiesFrom(BffTestHost other)
    {
        Cookies.Clear();
        foreach (var (name, value) in other.Cookies)
        {
            Cookies[name] = value;
        }
    }

    private void StoreCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            return;

        foreach (var setCookie in setCookies)
        {
            var parts = setCookie.Split(';', StringSplitOptions.TrimEntries);
            var separator = parts[0].IndexOf('=');
            if (separator <= 0)
                continue;

            var name = parts[0][..separator];
            var value = parts[0][(separator + 1)..];
            var isExpired = parts.Any(part =>
                part.Equals("max-age=0", StringComparison.OrdinalIgnoreCase)
            );
            if (isExpired || value.Length == 0)
                Cookies.Remove(name);
            else
                Cookies[name] = value;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
    }
}
