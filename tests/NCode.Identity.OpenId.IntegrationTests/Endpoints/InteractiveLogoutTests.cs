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
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

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
    private const string InteractiveCookieName = "NCode.Identity.Interactive";
    private const string AntiforgeryRequestCookieName = "XSRF-TOKEN";
    private const string AntiforgeryFieldName = "__RequestVerificationToken";
    private const string AntiforgeryCookiePrefix = ".AspNetCore.Antiforgery";

    [Fact]
    public async Task GetMe_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InteractiveLogout_WhenAuthenticated_ReturnsSessionThenClearsCookie()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

        var sessionCookie = await LoginAsync(client);
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
            (InteractiveCookieName, sessionCookie)
        );
        using var logout = await PostLogoutAsync(
            client,
            requestToken,
            cookieToken,
            (InteractiveCookieName, sessionCookie)
        );

        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        Assert.Equal(LoginPath, logout.Headers.Location?.OriginalString);

        // The OP login cookie is cleared (an expired Set-Cookie for the interactive scheme).
        var clearing = ReadSetCookieRaw(logout, InteractiveCookieName);
        Assert.NotNull(clearing);
    }

    #region Login Helpers

    private static async Task<string?> LoginAsync(HttpClient client)
    {
        var (requestToken, cookieToken) = await GetAntiforgeryTokensAsync(
            client,
            sessionCookie: null
        );
        using var login = await PostLoginAsync(client, requestToken, cookieToken);
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        return ReadSetCookieValue(login, InteractiveCookieName);
    }

    private static async Task<(
        string? RequestToken,
        string? CookieToken
    )> GetAntiforgeryTokensAsync(HttpClient client, (string Name, string? Value)? sessionCookie)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{LoginPath}?ReturnUrl={Uri.EscapeDataString(ReturnUrl)}"
        );
        if (sessionCookie is { Value: { } value })
        {
            request.Headers.Add("Cookie", $"{sessionCookie.Value.Name}={value}");
        }

        using var response = await client.SendAsync(request);

        var requestToken = ReadSetCookieValue(response, AntiforgeryRequestCookieName);
        var cookieToken = ReadSetCookieRaw(response, AntiforgeryCookiePrefix);
        return (requestToken, cookieToken);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string? requestToken,
        string? cookieToken
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LoginPath);
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

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostLogoutAsync(
        HttpClient client,
        string? requestToken,
        string? cookieToken,
        (string Name, string? Value)? sessionCookie
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LogoutPath);
        AddAntiforgeryCookies(request, requestToken, cookieToken, sessionCookie);

        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ReturnUrl"] = LoginPath,
                [AntiforgeryFieldName] = requestToken ?? string.Empty,
            }
        );

        return client.SendAsync(request);
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
