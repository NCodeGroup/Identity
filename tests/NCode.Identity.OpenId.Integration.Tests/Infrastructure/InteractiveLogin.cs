// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Web;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// Signs an end-user in through the interactive login page and returns the OP session cookie, so a test can drive the
/// authorization endpoint as an authenticated user agent.
/// </summary>
public static class InteractiveLogin
{
    /// <summary>
    /// The name of the OP session cookie the interactive login issues.
    /// </summary>
    public const string SessionCookieName = "__Host-NCode.Identity.Interactive";

    private const string LoginPath = "/account/login";
    private const string ReturnUrl = "/foo";
    private const string AntiforgeryRequestCookieName = "__Host-XSRF-TOKEN";
    private const string AntiforgeryFieldName = "__RequestVerificationToken";
    private const string AntiforgeryCookiePrefix = "__Host-NCode.Identity.Antiforgery";

    /// <summary>
    /// The path of the interactive surface's logout confirmation page, which an end-session request that needs the
    /// end-user's confirmation redirects to.
    /// </summary>
    public const string LogoutConfirmPath = "/account/logout/confirm";

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

    /// <summary>
    /// Sends an end-session request that needs the end-user's confirmation and answers the confirmation page the way a
    /// browser does: it asserts the request redirects to the logout confirmation page carrying the one-time continue
    /// URL, then posts the answer to it. The caller disposes the response.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host, with automatic redirects and cookies off.</param>
    /// <param name="endSessionUrl">The URL of the end-session request, with its query.</param>
    /// <param name="sessionCookie">The value of the session cookie to send.</param>
    /// <param name="action">The <c>action</c> the form posts, <c>logout</c> to confirm or <c>cancel</c> to decline.</param>
    /// <returns>The response of the confirmation form post.</returns>
    public static async Task<HttpResponseMessage> AnswerEndSessionConfirmationAsync(
        HttpClient client,
        string endSessionUrl,
        string sessionCookie,
        string action
    )
    {
        using var challenge = await GetWithSessionAsync(client, endSessionUrl, sessionCookie);
        Assert.Equal(HttpStatusCode.Found, challenge.StatusCode);
        Assert.NotNull(challenge.Headers.Location);

        var location = new Uri(
            new Uri("http://localhost"),
            challenge.Headers.Location.OriginalString
        );
        Assert.Equal(LogoutConfirmPath, location.AbsolutePath);

        var returnUrl = HttpUtility.ParseQueryString(location.Query)["ReturnUrl"];
        Assert.NotNull(returnUrl);
        Assert.NotEmpty(returnUrl);

        return await PostFormAsync(
            client,
            location.PathAndQuery,
            LogoutConfirmPath,
            sessionCookie,
            new Dictionary<string, string> { ["action"] = action, ["ReturnUrl"] = returnUrl }
        );
    }

    /// <summary>
    /// Sends an end-session request that needs the end-user's confirmation, confirms it on the confirmation page, and
    /// follows the redirect to the one-time continue URL that resumes the logout. The caller disposes the response.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host, with automatic redirects and cookies off.</param>
    /// <param name="endSessionUrl">The URL of the end-session request, with its query.</param>
    /// <param name="sessionCookie">The value of the session cookie to send.</param>
    /// <returns>The response of the resumed logout.</returns>
    public static async Task<HttpResponseMessage> ConfirmEndSessionAsync(
        HttpClient client,
        string endSessionUrl,
        string sessionCookie
    )
    {
        using var confirmation = await AnswerEndSessionConfirmationAsync(
            client,
            endSessionUrl,
            sessionCookie,
            "logout"
        );
        Assert.Equal(HttpStatusCode.Found, confirmation.StatusCode);
        Assert.NotNull(confirmation.Headers.Location);

        var continueUrl = new Uri(
            new Uri("http://localhost"),
            confirmation.Headers.Location.OriginalString
        );
        return await GetWithSessionAsync(client, continueUrl.PathAndQuery, sessionCookie);
    }

    private static Task<HttpResponseMessage> GetWithSessionAsync(
        HttpClient client,
        string url,
        string sessionCookie
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionCookie}");
        return client.SendAsync(request);
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
