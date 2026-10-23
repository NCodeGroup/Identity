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
        using var login = await SubmitLoginAsync(client, userName, password, ReturnUrl);
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);

        var sessionCookie = ReadSessionCookie(login);
        Assert.NotNull(sessionCookie);
        return sessionCookie;
    }

    /// <summary>
    /// Posts the interactive login form for <paramref name="userName"/> and returns the response, whatever its status,
    /// so a test can assert a refusal as well as a sign-in. The caller disposes the response.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host, with automatic redirects and cookies off.</param>
    /// <param name="userName">The user name of a local account.</param>
    /// <param name="password">The password sent for the account.</param>
    /// <param name="returnUrl">The local URL the login page was asked to return to.</param>
    /// <returns>The response of the login form post.</returns>
    public static Task<HttpResponseMessage> SubmitLoginAsync(
        HttpClient client,
        string userName,
        string password,
        string returnUrl
    ) =>
        PostFormAsync(
            client,
            $"{LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}",
            LoginPath,
            sessionCookie: null,
            new Dictionary<string, string>
            {
                ["username"] = userName,
                ["password"] = password,
                ["ReturnUrl"] = returnUrl,
            }
        );

    /// <summary>
    /// Reads the value of the session cookie that a response sets.
    /// </summary>
    /// <param name="response">The response of a sign-in or a form post.</param>
    /// <returns>The value of the <see cref="SessionCookieName"/> cookie, or <see langword="null"/> when the response
    /// does not set it.</returns>
    public static string? ReadSessionCookie(HttpResponseMessage response) =>
        ReadSetCookieValue(response, SessionCookieName);

    /// <summary>
    /// Posts a form of the interactive surface the way a browser does: it loads the page to obtain the antiforgery
    /// cookies (a page the host serves no bundle for answers <c>503</c> but still sets them), then posts the fields with
    /// the antiforgery token. The caller disposes the response.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host, with automatic redirects and cookies off.</param>
    /// <param name="pageUrl">The URL of the page that renders the form.</param>
    /// <param name="actionPath">The path the form posts to.</param>
    /// <param name="sessionCookie">The value of the session cookie to send, or <see langword="null"/> to send none.</param>
    /// <param name="fields">The form fields, without the antiforgery token.</param>
    /// <returns>The response of the form post.</returns>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string pageUrl,
        string actionPath,
        string? sessionCookie,
        IReadOnlyDictionary<string, string> fields
    )
    {
        using var pageRequest = new HttpRequestMessage(HttpMethod.Get, pageUrl);
        if (sessionCookie is not null)
            pageRequest.Headers.Add("Cookie", $"{SessionCookieName}={sessionCookie}");

        using var page = await client.SendAsync(pageRequest);
        var requestToken = ReadSetCookieValue(page, AntiforgeryRequestCookieName);
        var cookieToken = ReadSetCookieRaw(page, AntiforgeryCookiePrefix);

        using var request = new HttpRequestMessage(HttpMethod.Post, actionPath);
        List<string> cookies = [];
        if (sessionCookie is not null)
            cookies.Add($"{SessionCookieName}={sessionCookie}");
        if (cookieToken is not null)
            cookies.Add(cookieToken);
        if (requestToken is not null)
            cookies.Add($"{AntiforgeryRequestCookieName}={Uri.EscapeDataString(requestToken)}");
        if (cookies.Count > 0)
            request.Headers.Add("Cookie", string.Join("; ", cookies));

        var form = fields.ToDictionary(field => field.Key, field => field.Value);
        form[AntiforgeryFieldName] = requestToken ?? string.Empty;
        request.Content = new FormUrlEncodedContent(form);

        return await client.SendAsync(request);
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
