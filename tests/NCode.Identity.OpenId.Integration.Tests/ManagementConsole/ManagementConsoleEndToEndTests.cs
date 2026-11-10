// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff;
using NCode.Identity.ManagementConsole;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.ManagementConsole;

/// <summary>
/// Proves the management console's host end to end against the NCode OpenID provider hosted in-process: the console
/// logs in as the <c>mgmt-console</c> client that the provider's developer seed registers, with a private_key_jwt,
/// pushed authorization requests and DPoP.
/// </summary>
public class ManagementConsoleEndToEndTests
{
    private const string UserName = "console-e2e-user";
    private const string Password = "console-e2e-password";
    private const string CallbackUri = "https://localhost:5005/bff/callback";
    private const string SessionCookieName = "__Host-Http-bff.mgmt";

    private static string CreateClientJwks(ECDsa key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var x = Base64Url.EncodeToString(parameters.Q.X!);
        var y = Base64Url.EncodeToString(parameters.Q.Y!);
        return $$"""{"keys":[{"kty":"EC","crv":"P-256","x":"{{x}}","y":"{{y}}","kid":"mgmt-client","use":"sig","alg":"ES256"}]}""";
    }

    private static async ValueTask<HttpResponseMessage> SendAsync(
        HttpClient client,
        Dictionary<string, string> cookies,
        HttpMethod method,
        string pathAndQuery,
        IReadOnlyList<(string Name, string Value)> headers,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(method, pathAndQuery);
        if (cookies.Count > 0)
        {
            request.Headers.Add(
                "Cookie",
                string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}"))
            );
        }

        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        var response = await client.SendAsync(request, cancellationToken);
        StoreCookies(cookies, response);
        return response;
    }

    private static void StoreCookies(
        Dictionary<string, string> cookies,
        HttpResponseMessage response
    )
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
                cookies.Remove(name);
            else
                cookies[name] = value;
        }
    }

    #region Login Tests

    [Fact]
    public async Task Login_ThroughTheProvider_CreatesTheConsoleSession()
    {
        using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provider = new ManagementConsoleSeedingHostApplicationFactory(
            CreateClientJwks(clientKey)
        );
        await provider.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);

        using var providerBrowser = provider.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var baseConsole = new ManagementConsoleApplicationFactory(provider, clientKey);
        await using var console = baseConsole.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection
                    .AddHttpClient(BffDefaults.RemoteApiHttpClientName)
                    .AddHttpMessageHandler(() => new RecordingHttpMessageHandler(exchanges))
            )
        );
        using var consoleBrowser = console.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:5005/"),
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var consoleCookies = new Dictionary<string, string>(StringComparer.Ordinal);

        var providerSession = await InteractiveLogin.SignInAsync(
            providerBrowser,
            UserName,
            Password,
            CancellationToken.None
        );

        using var login = await SendAsync(
            consoleBrowser,
            consoleCookies,
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/login?returnUrl=%2Fteams",
            [],
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorizeUri = login.Headers.Location;
        Assert.NotNull(authorizeUri);

        using var authorizeRequest = new HttpRequestMessage(HttpMethod.Get, authorizeUri);
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={providerSession}"
        );
        using var authorize = await providerBrowser.SendAsync(authorizeRequest);
        Assert.Equal(HttpStatusCode.SeeOther, authorize.StatusCode);
        var callbackUri = authorize.Headers.Location;
        Assert.NotNull(callbackUri);
        Assert.StartsWith(CallbackUri, callbackUri.OriginalString, StringComparison.Ordinal);

        using var callback = await SendAsync(
            consoleBrowser,
            consoleCookies,
            HttpMethod.Get,
            callbackUri.PathAndQuery,
            [],
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/teams", callback.Headers.Location?.OriginalString);
        Assert.Contains(SessionCookieName, consoleCookies.Keys);

        using var user = await SendAsync(
            consoleBrowser,
            consoleCookies,
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/user",
            [(BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue)],
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
        var body = JsonElement.Parse(await user.Content.ReadAsStringAsync());
        Assert.True(body.TryGetProperty("sub", out _));
        Assert.True(body.TryGetProperty("logoutSid", out _));

        using var management = await SendAsync(
            consoleBrowser,
            consoleCookies,
            HttpMethod.Get,
            $"{ManagementConsoleDefaults.ApiPath}/me",
            [(BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue)],
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, management.StatusCode);
        var caller = JsonElement.Parse(await management.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, caller.ValueKind);

        // The provider requires no DPoP nonce, so the console's one management request reaches it once.
        var call = Assert.Single(exchanges);
        Assert.Equal("/api/me", call.Path);
        Assert.Equal("DPoP", call.AuthorizationScheme);
        Assert.Equal(HttpStatusCode.OK, call.StatusCode);
        var proof = Assert.IsType<string>(call.DPoPProof);
        var proofSegments = proof.Split('.');
        Assert.Equal(3, proofSegments.Length);
        var payloadBytes = Base64Url.DecodeFromChars(proofSegments[1]);
        var payload = JsonElement.Parse(payloadBytes);
        var htu = payload.GetProperty("htu").GetString();
        Assert.Equal("http://localhost/api/me", htu);
    }

    #endregion
}
