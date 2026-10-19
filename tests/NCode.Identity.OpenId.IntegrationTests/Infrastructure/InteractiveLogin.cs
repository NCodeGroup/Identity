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
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// Signs an end-user in through the interactive login page and returns the OP session cookie, so a test can drive the
/// authorization endpoint as an authenticated user agent.
/// </summary>
public static class InteractiveLogin
{
    /// <summary>
    /// The name of the OP session cookie the interactive login issues.
    /// </summary>
    public const string SessionCookieName = "NCode.Identity.Interactive";

    private const string LoginPath = "/account/login";
    private const string ReturnUrl = "/foo";
    private const string AntiforgeryRequestCookieName = "XSRF-TOKEN";
    private const string AntiforgeryFieldName = "__RequestVerificationToken";
    private const string AntiforgeryCookiePrefix = ".AspNetCore.Antiforgery";

    /// <summary>
    /// Signs <paramref name="userName"/> in and returns the value of the session cookie. The client must not handle
    /// cookies itself, so the test sends the cookie explicitly.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host, with automatic redirects and cookies off.</param>
    /// <param name="userName">The user name of a seeded local account.</param>
    /// <param name="password">The password of the account.</param>
    /// <returns>The value of the <see cref="SessionCookieName"/> cookie.</returns>
    public static async Task<string> SignInAsync(
        HttpClient client,
        string userName,
        string password
    )
    {
        using var loginPage = await client.GetAsync(
            $"{LoginPath}?ReturnUrl={Uri.EscapeDataString(ReturnUrl)}"
        );
        var requestToken = ReadSetCookieValue(loginPage, AntiforgeryRequestCookieName);
        var cookieToken = ReadSetCookieRaw(loginPage, AntiforgeryCookiePrefix);

        using var request = new HttpRequestMessage(HttpMethod.Post, LoginPath);
        List<string> cookies = [];
        if (cookieToken is not null)
            cookies.Add(cookieToken);
        if (requestToken is not null)
            cookies.Add($"{AntiforgeryRequestCookieName}={Uri.EscapeDataString(requestToken)}");
        if (cookies.Count > 0)
            request.Headers.Add("Cookie", string.Join("; ", cookies));
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["username"] = userName,
                ["password"] = password,
                ["ReturnUrl"] = ReturnUrl,
                [AntiforgeryFieldName] = requestToken ?? string.Empty,
            }
        );

        using var login = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);

        var sessionCookie = ReadSetCookieValue(login, SessionCookieName);
        Assert.NotNull(sessionCookie);
        return sessionCookie;
    }

    private static string? ReadSetCookieValue(HttpResponseMessage response, string name)
    {
        var raw = ReadSetCookieRaw(response, name);
        if (raw is null)
            return null;

        var value = raw[(raw.IndexOf('=') + 1)..];
        return Uri.UnescapeDataString(value);
    }

    private static string? ReadSetCookieRaw(HttpResponseMessage response, string namePrefix)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        return cookies
            .Select(cookie => cookie.Split(';')[0])
            .FirstOrDefault(firstPart =>
                firstPart.StartsWith(namePrefix, StringComparison.Ordinal)
            );
    }
}
