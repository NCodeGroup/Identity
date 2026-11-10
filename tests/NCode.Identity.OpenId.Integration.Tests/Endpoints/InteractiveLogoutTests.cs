// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Exercises the first-party interactive account surface end-to-end: an interactive login establishes the OP session
/// cookie, <c>GET /account/me</c> returns the current session as JSON, and an antiforgery-protected
/// <c>POST /account/logout</c> runs the same server-side logout pipeline as the <c>end_session_endpoint</c> — clearing
/// the login cookie and redirecting to the validated local return URL.
/// </summary>
public class InteractiveLogoutTests
{
    private const string UserName = "admin";
    private const string Password = "dev-password";
    private const string ReturnUrl = "/foo";

    private const string MePath = "/account/me";
    private const string LogoutPath = "/account/logout";
    private const string LoginPath = "/account/login";
    private const string InteractiveCookieName = "__Host-NCode.Identity.Interactive";
    private const string AntiforgeryRequestCookieName = "__Host-XSRF-TOKEN";
    private const string AntiforgeryFieldName = "__RequestVerificationToken";
    private const string AntiforgeryCookiePrefix = "__Host-NCode.Identity.Antiforgery";

    [Fact]
    public async Task GetMe_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InteractiveLogout_WhenAuthenticated_ReturnsSessionThenClearsCookie()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

        var sessionCookie = await LoginAsync(client, CancellationToken.None);
        Assert.NotNull(sessionCookie);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, MePath);
        meRequest.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        using var meResponse = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal("application/json", meResponse.Content.Headers.ContentType?.MediaType);
        var meBody = await meResponse.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(meBody));

        // Mint antiforgery tokens bound to the authenticated session, then POST the logout.
        var (requestToken, cookieToken) = await GetAntiforgeryTokensAsync(
            client,
            (InteractiveCookieName, sessionCookie),
            CancellationToken.None
        );
        using var logout = await PostLogoutAsync(
            client,
            requestToken,
            cookieToken,
            (InteractiveCookieName, sessionCookie),
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        Assert.Equal(LoginPath, logout.Headers.Location?.OriginalString);

        // The OP login cookie is cleared (an expired Set-Cookie for the interactive scheme).
        var clearing = ReadSetCookieRaw(logout, InteractiveCookieName);
        Assert.NotNull(clearing);
    }

    [Fact]
    public async Task ConcurrentLogins_ForSameAccount_EachSessionSucceedsWithPromptNone()
    {
        const string clientId = "it-concurrent-prompt-none";
        const string redirectUri = "https://client.example/cb";

        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientAsync(clientId, [redirectUri], CancellationToken.None);

        const int concurrency = 32;
        var failures = new ConcurrentBag<string>();

        await Task.WhenAll(
            Enumerable
                .Range(0, concurrency)
                .Select(async i =>
                {
                    var client = factory.CreateClient(
                        new WebApplicationFactoryClientOptions
                        {
                            AllowAutoRedirect = false,
                            HandleCookies = false,
                        }
                    );

                    var sessionCookie = await LoginAsync(client, CancellationToken.None);
                    if (string.IsNullOrEmpty(sessionCookie))
                    {
                        failures.Add($"request {i}: login issued no session cookie");
                        return;
                    }

                    // prompt=none re-authorization must silently succeed for the session just established, echoing a
                    // code back to the client — never login_required. Regression guard for a value-type-default bug
                    // where subject_max_age materialized as 0 and forced re-authentication on every request.
                    var query =
                        $"?client_id={clientId}"
                        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                        + "&response_type=code"
                        + "&scope=openid"
                        + $"&state=state-{i:D2}"
                        + "&prompt=none"
                        + TestPkce.QueryParameters;
                    using var request = new HttpRequestMessage(
                        HttpMethod.Get,
                        "/oauth2/authorize" + query
                    );
                    request.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
                    using var response = await client.SendAsync(request);

                    var parameters = response.Headers.Location is { } location
                        ? HttpUtility.ParseQueryString(location.Query)
                        : null;
                    var error = parameters?["error"];
                    var code = parameters?["code"];
                    if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
                    {
                        failures.Add(
                            $"request {i}: prompt=none returned error='{error ?? "<none>"}' code='{code ?? "<none>"}'"
                        );
                    }
                })
        );

        Assert.True(failures.IsEmpty, string.Join("; ", failures));
    }

    #region Login Helpers

    private static async ValueTask<string?> LoginAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        var (requestToken, cookieToken) = await GetAntiforgeryTokensAsync(
            client,
            sessionCookie: null,
            cancellationToken
        );
        using var login = await PostLoginAsync(
            client,
            requestToken,
            cookieToken,
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        return ReadSetCookieValue(login, InteractiveCookieName);
    }

    private static async ValueTask<(
        string? RequestToken,
        string? CookieToken
    )> GetAntiforgeryTokensAsync(
        HttpClient client,
        (string Name, string? Value)? sessionCookie,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{LoginPath}?ReturnUrl={Uri.EscapeDataString(ReturnUrl)}"
        );
        if (sessionCookie is { Value: { } value })
        {
            request.Headers.Add("Cookie", $"{sessionCookie.Value.Name}={value}");
        }

        using var response = await client.SendAsync(request, cancellationToken);

        var requestToken = ReadSetCookieValue(response, AntiforgeryRequestCookieName);
        var cookieToken = ReadSetCookieRaw(response, AntiforgeryCookiePrefix);
        return (requestToken, cookieToken);
    }

    private static async ValueTask<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string? requestToken,
        string? cookieToken,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginPath);
        AddAntiforgeryCookies(request, requestToken, cookieToken, sessionCookie: null);

        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["username"] = UserName,
                ["password"] = Password,
                ["ReturnUrl"] = ReturnUrl,
                [AntiforgeryFieldName] = requestToken ?? string.Empty,
            }
        );

        return await client.SendAsync(request, cancellationToken);
    }

    private static async ValueTask<HttpResponseMessage> PostLogoutAsync(
        HttpClient client,
        string? requestToken,
        string? cookieToken,
        (string Name, string? Value)? sessionCookie,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LogoutPath);
        AddAntiforgeryCookies(request, requestToken, cookieToken, sessionCookie);

        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ReturnUrl"] = LoginPath,
                [AntiforgeryFieldName] = requestToken ?? string.Empty,
            }
        );

        return await client.SendAsync(request, cancellationToken);
    }

    private static void AddAntiforgeryCookies(
        HttpRequestMessage request,
        string? requestToken,
        string? cookieToken,
        (string Name, string? Value)? sessionCookie
    )
    {
        var cookies = new List<string>();
        if (cookieToken is not null)
        {
            cookies.Add(cookieToken);
        }
        if (requestToken is not null)
        {
            cookies.Add($"{AntiforgeryRequestCookieName}={Uri.EscapeDataString(requestToken)}");
        }
        if (sessionCookie is { Value: { } sessionValue })
        {
            cookies.Add($"{sessionCookie.Value.Name}={sessionValue}");
        }

        if (cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookies));
        }
    }

    private static string? ReadSetCookieValue(HttpResponseMessage response, string name)
    {
        var raw = ReadSetCookieRaw(response, name);
        if (raw is null)
        {
            return null;
        }

        var value = raw[(raw.IndexOf('=') + 1)..];
        return Uri.UnescapeDataString(value);
    }

    private static string? ReadSetCookieRaw(HttpResponseMessage response, string namePrefix)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            var firstPart = cookie.Split(';')[0];
            if (firstPart.StartsWith(namePrefix, StringComparison.Ordinal))
            {
                return firstPart;
            }
        }

        return null;
    }

    #endregion
}
