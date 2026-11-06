// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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

    // The console forwards the management API through its own socket handler, which a TestServer cannot receive, so the
    // console's management API URL names a loopback listener that relays each call to the provider's TestServer and
    // records the credentials the console presented.
    private static async Task<WebApplication> StartRelayAsync(
        HttpClient providerClient,
        ConcurrentQueue<(string Authorization, string Proof)> requests
    )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Run(async context =>
        {
            var target =
                $"http://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
            using var forward = new HttpRequestMessage(
                new HttpMethod(context.Request.Method),
                target
            );
            foreach (var header in context.Request.Headers)
            {
                if (!string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                {
                    forward.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            requests.Enqueue(
                (
                    context.Request.Headers.Authorization.ToString(),
                    context.Request.Headers["DPoP"].ToString()
                )
            );

            using var response = await providerClient.SendAsync(forward);
            context.Response.StatusCode = (int)response.StatusCode;
            var body = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            await context.Response.Body.WriteAsync(body, context.RequestAborted);
        });

        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Dictionary<string, string> cookies,
        HttpMethod method,
        string pathAndQuery,
        params (string Name, string Value)[] headers
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

        var response = await client.SendAsync(request);
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
        await provider.SeedLocalAccountAsync(UserName, Password);

        using var providerBrowser = provider.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var managementRequests = new ConcurrentQueue<(string Authorization, string Proof)>();
        await using var relay = await StartRelayAsync(providerBrowser, managementRequests);
        await using var baseConsole = new ManagementConsoleApplicationFactory(provider, clientKey);
        await using var console = baseConsole.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ManagementConsole:ManagementApiUrl"] = $"{relay.Urls.First()}/api",
                    }
                )
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
            Password
        );

        using var login = await SendAsync(
            consoleBrowser,
            consoleCookies,
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/login?returnUrl=%2Fteams"
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
            callbackUri.PathAndQuery
        );
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/teams", callback.Headers.Location?.OriginalString);
        Assert.Contains(SessionCookieName, consoleCookies.Keys);

        using var user = await SendAsync(
            consoleBrowser,
            consoleCookies,
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/user",
            (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue)
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
            (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue)
        );
        Assert.Equal(HttpStatusCode.OK, management.StatusCode);
        var caller = JsonElement.Parse(await management.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, caller.ValueKind);
        var request = Assert.Single(managementRequests);
        Assert.StartsWith("DPoP ", request.Authorization, StringComparison.Ordinal);
        Assert.NotEmpty(request.Proof);
    }

    #endregion
}
