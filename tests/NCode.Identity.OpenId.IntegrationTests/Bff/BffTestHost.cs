#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections.Concurrent;
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
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.IntegrationTests.Bff;

/// <summary>
/// Hosts a minimal backend-for-frontend (BFF) application in-process, wired to the NCode OpenID provider of a
/// <see cref="HostApplicationFactory"/>, and drives it as a browser with a cookie jar.
/// </summary>
internal sealed class BffTestHost : IAsyncDisposable
{
    public const string ClientId = "it-bff-e2e";
    public const string ClientSecret = "bff-e2e-Wq4mZ8vK2nT7xR5bL9cY3hF6jD1sGp0E";
    public const string KeyRing = "bff-e2e";
    public const string ApiTokenPath = "/api/token";

    private static readonly Uri BaseAddress = new("https://bff.example/");

    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    private BffTestHost(WebApplication app, HttpClient client)
    {
        _app = app;
        _client = client;
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
        var services = new ServiceCollection();
        services
            .AddSharedStateServices()
            .AddInProcessExpiringKeySet()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        var provider = services.BuildServiceProvider();
        return (
            provider.GetRequiredService<IExpiringKeySet>(),
            provider.GetRequiredService<IExpiringValueSet>(),
            provider.GetRequiredService<IExpiringIndexSet>()
        );
    }

    /// <summary>
    /// Builds and starts a BFF host.
    /// </summary>
    public static async Task<BffTestHost> StartAsync(
        HostApplicationFactory factory,
        IExpiringKeySet keySet,
        IExpiringValueSet valueSet,
        IExpiringIndexSet indexSet,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null,
        ConcurrentQueue<RecordedHttpExchange>? exchanges = null,
        bool authenticateBeforeBff = false
    )
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
            }
        );
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Bff:Authority"] = HostApplicationFactory.TestServerIssuer,
                ["Bff:ClientId"] = ClientId,
                ["Bff:ClientAuthentication:Method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                ["Bff:ClientAuthentication:KeyRing"] = KeyRing,
                ["Bff:ReturnUrls:0"] = "/",
                ["Bff:LoginParameters:0"] = OpenIdProtocol.Parameters.Prompt,
                ["Bff:UserClaims:0"] = "email",
            }
        );

        if (extraConfiguration is not null)
            builder.Configuration.AddInMemoryCollection(extraConfiguration);

        var services = builder.Services;
        services.AddSingleton(keySet);
        services.AddSingleton(valueSet);
        services.AddSingleton(indexSet);
        services.AddKeyedSingleton<ISecretKeyCollectionProvider>(
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

        // The replicas share the state registered above; the turnkey composes everything else.
        services.AddIdentityBff(builder.Configuration);

        // Registered after the library, which configures its own primary handler for the named client.
        var httpClientBuilder = services
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

        await app.StartAsync();
        var server = app.GetTestServer();
        server.BaseAddress = BaseAddress;
        var client = server.CreateClient();
        return new BffTestHost(app, client);
    }

    /// <summary>
    /// Sends a request with the cookies of the jar and stores the cookies that the response sets.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string pathAndQuery,
        params (string Name, string Value)[] headers
    )
    {
        using var request = new HttpRequestMessage(method, pathAndQuery);
        if (_cookies.Count > 0)
        {
            var cookie = string.Join("; ", _cookies.Select(pair => $"{pair.Key}={pair.Value}"));
            request.Headers.Add("Cookie", cookie);
        }

        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        var response = await _client.SendAsync(request);
        StoreCookies(response);
        return response;
    }

    /// <summary>
    /// Removes every cookie from the jar.
    /// </summary>
    public void ClearCookies() => _cookies.Clear();

    /// <summary>
    /// Posts a form without the cookies of the jar, as a server-to-server call from the provider.
    /// </summary>
    public async Task<HttpResponseMessage> PostFormAsync(string path, HttpContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = content;
        return await _client.SendAsync(request);
    }

    /// <summary>
    /// Replaces the cookies of the jar with a copy of the cookies of another host's jar, as a browser that sends its
    /// cookie to a different replica of the same application.
    /// </summary>
    public void CopyCookiesFrom(BffTestHost other)
    {
        _cookies.Clear();
        foreach (var (name, value) in other._cookies)
        {
            _cookies[name] = value;
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
                _cookies.Remove(name);
            else
                _cookies[name] = value;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }
}
