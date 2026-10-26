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

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Proves that every page the server writes carries its Content-Security-Policy and security headers, and that only
/// the <c>check_session_iframe</c> may be framed.
/// </summary>
// See ADR-0256.
public class HtmlPageSecurityTests
{
    private const string UserName = "page-user";
    private const string Password = "page-security-passphrase";
    private const string RedirectUri = "https://client.example/callback";
    private const string AntiforgeryCookiePrefix = ".AspNetCore.Antiforgery";

    private const string SurfacePolicyPattern =
        @"^default-src 'none'; script-src 'nonce-([A-Za-z0-9_-]{22})' 'strict-dynamic'; style-src 'nonce-\1'; img-src 'self'; connect-src 'self'; base-uri 'self'; frame-ancestors 'none'\z";

    private const string NonceBundleIndexHtml =
        "<!doctype html><html><head><meta property=\"csp-nonce\" nonce=\"__NCODE_CSP_NONCE__\">"
        + "<script type=\"module\" src=\"./assets/app.js\" nonce=\"__NCODE_CSP_NONCE__\"></script></head>"
        + "<body><div id=\"root\"></div></body></html>";

    #region Helpers

    private static HttpClient CreateClient(WebApplicationFactory<HostApiMarker> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async Task<HttpResponseMessage> GetWithSessionAsync(
        HttpClient client,
        string url,
        string sessionCookie
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request);
    }

    private static string? ReadAntiforgeryCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies
                .Select(cookie => cookie.Split(';')[0])
                .FirstOrDefault(firstPart =>
                    firstPart.StartsWith(AntiforgeryCookiePrefix, StringComparison.Ordinal)
                )
            : null;

    private static string AssertFramingForbidden(HttpResponseMessage response)
    {
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal(
            "nosniff",
            Assert.Single(response.Headers.GetValues("X-Content-Type-Options"))
        );
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.True(response.Headers.CacheControl?.NoStore);
        return Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
    }

    private static string HashScript(string html)
    {
        const string scriptStart = "<script>";
        const string scriptEnd = "</script>";

        var startIndex = html.IndexOf(scriptStart, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, html);
        var scriptIndex = startIndex + scriptStart.Length;
        var endIndex = html.IndexOf(scriptEnd, scriptIndex, StringComparison.Ordinal);
        Assert.True(endIndex >= 0, html);

        var script = html[scriptIndex..endIndex];
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
    }

    #endregion

    #region Interactive Page Tests

    [Fact]
    public async Task GetLogin_WhenTheBrowserIsNew_ServesTheSurfacePolicy()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/account/login?ReturnUrl=%2Ffoo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(SurfacePolicyPattern, AssertFramingForbidden(response));
    }

    [Fact]
    public async Task GetLogin_WhenTheBrowserReturnsWithItsAntiforgeryCookie_StillForbidsFraming()
    {
        const string loginUrl = "/account/login?ReturnUrl=%2Ffoo";
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var first = await client.GetAsync(loginUrl);
        var antiforgeryCookie = ReadAntiforgeryCookie(first);
        Assert.NotNull(antiforgeryCookie);

        using var request = new HttpRequestMessage(HttpMethod.Get, loginUrl);
        request.Headers.Add("Cookie", antiforgeryCookie);
        using var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // Antiforgery reuses the cookie it was sent, so it adds no frame option of its own (issue #303).
        Assert.Null(ReadAntiforgeryCookie(second));
        Assert.Matches(SurfacePolicyPattern, AssertFramingForbidden(second));
    }

    [Fact]
    public async Task GetConsent_WhenAConsentIsPending_ForbidsFraming()
    {
        const string clientId = "it-page-consent";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object> { [OpenIdSettingNames.RequireConsent] = true },
            RedirectUri
        );
        using var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters;
        using var authorizeResponse = await GetWithSessionAsync(
            client,
            "/oauth2/authorize" + query,
            sessionCookie
        );

        Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
        var consentLocation = authorizeResponse.Headers.Location;
        Assert.NotNull(consentLocation);
        var consentUrl = new Uri(new Uri("http://localhost"), consentLocation);
        var returnUrl = HttpUtility.ParseQueryString(consentUrl.Query)["ReturnUrl"];
        Assert.False(string.IsNullOrEmpty(returnUrl), consentLocation.OriginalString);

        using var response = await GetWithSessionAsync(
            client,
            consentUrl.PathAndQuery,
            sessionCookie
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(SurfacePolicyPattern, AssertFramingForbidden(response));
    }

    [Fact]
    public async Task GetAuthorize_WhenTheErrorPageIsRendered_ServesTheSurfacePolicy()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/oauth2/authorize?client_id=unknown-client"
        );
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,*/*;q=0.8");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Matches(SurfacePolicyPattern, AssertFramingForbidden(response));
    }

    #endregion

    #region Bundle Page Tests

    [Fact]
    public async Task GetAccount_WhenTheBundleIsServed_WritesTheResponsesNonceIntoTheBundle()
    {
        var bundleRoot = Directory.CreateTempSubdirectory("bundle-");
        try
        {
            File.WriteAllText(
                Path.Combine(bundleRoot.FullName, "index.html"),
                NonceBundleIndexHtml
            );
            using var factory = new ConfiguredHostApplicationFactory(
                new Dictionary<string, string?>
                {
                    ["InteractiveAuthentication:BundleRootPath"] = bundleRoot.FullName,
                }
            );
            await factory.SeedLocalAccountAsync(UserName, Password);
            using var client = CreateClient(factory);
            var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

            List<string> nonces = [];
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var response = await GetWithSessionAsync(client, "/account", sessionCookie);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var policy = AssertFramingForbidden(response);
                var match = Regex.Match(policy, SurfacePolicyPattern);
                Assert.True(match.Success, policy);
                var nonce = match.Groups[1].Value;
                var html = await response.Content.ReadAsStringAsync();
                Assert.Contains(
                    $"<meta property=\"csp-nonce\" nonce=\"{nonce}\">",
                    html,
                    StringComparison.Ordinal
                );
                Assert.Contains(
                    $"<script type=\"module\" src=\"./assets/app.js\" nonce=\"{nonce}\"></script>",
                    html,
                    StringComparison.Ordinal
                );
                Assert.DoesNotContain("__NCODE_CSP_NONCE__", html, StringComparison.Ordinal);
                nonces.Add(nonce);
            }

            Assert.NotEqual(nonces[0], nonces[1]);
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    #endregion

    #region Protocol Page Tests

    [Fact]
    public async Task Authorize_WithFormPost_AllowsOnlyTheFormsScriptAndItsClientsFrame()
    {
        const string clientId = "it-page-form-post";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        using var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + "&prompt=none"
            + "&response_mode=form_post"
            + TestPkce.QueryParameters;
        using var response = await GetWithSessionAsync(
            client,
            "/oauth2/authorize" + query,
            sessionCookie
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(["same-origin"], response.Headers.GetValues("Referrer-Policy"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal(
            $"default-src 'none'; script-src 'sha256-{HashScript(html)}'; base-uri 'none'; frame-ancestors https://client.example",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy"))
        );
    }

    [Fact]
    public async Task GetCheckSession_Always_MayBeFramedAndRunsOnlyItsScript()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/oauth2/check_session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(
            $"default-src 'none'; script-src 'sha256-{HashScript(html)}'; base-uri 'none'; form-action 'none'",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy"))
        );
        Assert.False(response.Headers.Contains("X-Frame-Options"));
    }

    [Fact]
    public async Task GetEndSession_WhenFrontChannelLogoutUrisAreRendered_FramesOnlyTheirOrigins()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.FrontChannelLogoutSupported] = true,
            }
        );
        await factory.SeedLocalAccountAsync(UserName, Password);
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IFrontChannelLogoutService>(
                    new StubFrontChannelLogoutService()
                )
            )
        );
        using var client = CreateClient(host);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await GetWithSessionAsync(
            client,
            "/oauth2/end_session",
            sessionCookie
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "<iframe hidden src=\"https://rp.example/frontchannel?iss=x&amp;sid=y\"></iframe>",
            html,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "default-src 'none'; frame-src https://rp.example; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            AssertFramingForbidden(response)
        );
    }

    #endregion

    #region Stubs

    private sealed class StubFrontChannelLogoutService : IFrontChannelLogoutService
    {
        public ValueTask<IReadOnlyList<string>> GetLogoutUrisAsync(
            OpenIdContext openIdContext,
            EndSessionSubject subject,
            CancellationToken cancellationToken
        ) =>
            ValueTask.FromResult<IReadOnlyList<string>>([
                "https://rp.example/frontchannel?iss=x&sid=y",
            ]);
    }

    #endregion
}
