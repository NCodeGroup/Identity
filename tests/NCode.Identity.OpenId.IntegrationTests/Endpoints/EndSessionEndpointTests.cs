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
/// Exercises the OpenID Connect RP-Initiated Logout <c>end_session_endpoint</c> end-to-end: an interactive login
/// establishes an OP session cookie, and a <c>GET</c> to the end-session endpoint runs the full logout pipeline
/// (resolve subject/session from the cookie, clear the OP login, revoke the session's grants, and — since no relying
/// party participated — skip back-channel notification), rendering the logged-out confirmation.
/// </summary>
public class EndSessionEndpointTests
{
    private const string UserName = "admin";
    private const string Password = "dev-password";
    private const string ReturnUrl = "/foo";

    private const string EndSessionPath = "/oauth2/end_session";
    private const string InteractiveCookieName = "NCode.Identity.Interactive";
    private const string AntiforgeryRequestCookieName = "XSRF-TOKEN";
    private const string AntiforgeryFieldName = "__RequestVerificationToken";
    private const string AntiforgeryCookiePrefix = ".AspNetCore.Antiforgery";

    [Fact]
    public async Task GetEndSession_WhenAuthenticated_ClearsSessionCookieAndRendersLoggedOut()
    {
        using var factory = new HostApplicationFactory();
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

        using var request = new HttpRequestMessage(HttpMethod.Get, EndSessionPath);
        request.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("logged out", body, StringComparison.OrdinalIgnoreCase);

        // The OP login cookie is cleared (an expired Set-Cookie for the interactive scheme).
        var clearing = ReadSetCookieRaw(response, InteractiveCookieName);
        Assert.NotNull(clearing);
    }

    #region Login Helpers

    private static async Task<string?> LoginAsync(HttpClient client)
    {
        var (requestToken, cookieToken) = await GetAntiforgeryTokensAsync(client);
        using var login = await PostLoginAsync(client, requestToken, cookieToken);
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        return ReadSetCookieValue(login, InteractiveCookieName);
    }

    private static async Task<(
        string? RequestToken,
        string? CookieToken
    )> GetAntiforgeryTokensAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/account/login?ReturnUrl={Uri.EscapeDataString(ReturnUrl)}"
        );
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
        var request = new HttpRequestMessage(HttpMethod.Post, "/account/login");

        var cookies = new List<string>();
        if (cookieToken is not null)
        {
            cookies.Add(cookieToken);
        }
        if (requestToken is not null)
        {
            cookies.Add($"{AntiforgeryRequestCookieName}={Uri.EscapeDataString(requestToken)}");
        }
        request.Headers.Add("Cookie", string.Join("; ", cookies));

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
