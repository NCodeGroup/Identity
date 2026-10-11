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
/// Guards the request-pipeline ordering the interactive login surface depends on: ASP.NET Core
/// antiforgery tokens are claims-bound, so <c>UseAntiforgery</c> must run AFTER
/// <c>UseAuthentication</c>/<c>UseAuthorization</c>. The Playground registers a single cookie scheme,
/// which ASP.NET Core promotes to the default authenticate scheme — so once an interactive session
/// cookie exists, every request (including the login page) authenticates the user and the login form's
/// antiforgery token is minted bound to that user. If antiforgery validation runs before
/// authentication, the POST is validated against an anonymous user and fails with
/// "The provided antiforgery token was meant for a different claims-based user than the current user".
/// </summary>
public class InteractiveLoginAntiforgeryTests
{
    private const string UserName = "admin";
    private const string Password = "dev-password";
    private const string ReturnUrl = "/foo";

    private const string InteractiveCookieName = "NCode.Identity.Interactive";
    private const string AntiforgeryRequestCookieName = "XSRF-TOKEN";
    private const string AntiforgeryFieldName = "__RequestVerificationToken";
    private const string AntiforgeryCookiePrefix = ".AspNetCore.Antiforgery";

    [Fact]
    public async Task PostLogin_WhenAntiforgeryTokenMintedUnderAnExistingSessionCookie_Succeeds()
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

        var (firstRequestToken, firstCookieToken) = await GetAntiforgeryTokensAsync(
            client,
            sessionCookie: null
        );
        using var firstLogin = await PostLoginAsync(
            client,
            firstRequestToken,
            firstCookieToken,
            sessionCookie: null
        );

        Assert.Equal(HttpStatusCode.Found, firstLogin.StatusCode);
        var sessionCookie = ReadSetCookieValue(firstLogin, InteractiveCookieName);
        Assert.NotNull(sessionCookie);

        var (secondRequestToken, secondCookieToken) = await GetAntiforgeryTokensAsync(
            client,
            (InteractiveCookieName, sessionCookie)
        );
        using var secondLogin = await PostLoginAsync(
            client,
            secondRequestToken,
            secondCookieToken,
            (InteractiveCookieName, sessionCookie)
        );

        Assert.Equal(HttpStatusCode.Found, secondLogin.StatusCode);
        Assert.Equal(ReturnUrl, secondLogin.Headers.Location?.OriginalString);
    }

    private static async Task<(
        string? RequestToken,
        string? CookieToken
    )> GetAntiforgeryTokensAsync(HttpClient client, (string Name, string? Value)? sessionCookie)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/account/login?ReturnUrl={Uri.EscapeDataString(ReturnUrl)}"
        );
        AppendCookies(request, sessionCookie);

        using var response = await client.SendAsync(request);

        var requestToken = ReadSetCookieValue(response, AntiforgeryRequestCookieName);
        var cookieToken = ReadSetCookieRaw(response, AntiforgeryCookiePrefix);
        return (requestToken, cookieToken);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string? requestToken,
        string? cookieToken,
        (string Name, string? Value)? sessionCookie
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
        if (sessionCookie is { Value: { } sessionValue })
        {
            cookies.Add($"{sessionCookie.Value.Name}={sessionValue}");
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

    private static void AppendCookies(
        HttpRequestMessage request,
        (string Name, string? Value)? sessionCookie
    )
    {
        if (sessionCookie is { Value: { } value })
        {
            request.Headers.Add("Cookie", $"{sessionCookie.Value.Name}={value}");
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
}
