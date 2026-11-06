// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Cookies;

/// <summary>
/// Reads, writes and deletes the BFF session cookie and the login cookie.
/// </summary>
internal class BffCookies(IOptions<BffOptions> options, TimeProvider timeProvider)
{
    // 32 random bytes in unpadded base64url.
    private const int ValueLength = 43;

    /// <summary>
    /// Reads the session handle from the request, or returns <see langword="null"/> when the cookie is missing or
    /// malformed.
    /// </summary>
    /// <param name="request">The current request.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public string? ReadSessionHandle(HttpRequest request) =>
        Read(request, BffDefaults.SessionCookieName);

    /// <summary>
    /// Reads the login secret from the request, or returns <see langword="null"/> when the cookie is missing or
    /// malformed.
    /// </summary>
    /// <param name="request">The current request.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public string? ReadLoginSecret(HttpRequest request) =>
        Read(request, BffDefaults.LoginCookieName);

    /// <summary>
    /// Sets the session cookie so that it expires with the session.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="handle">The session handle.</param>
    /// <param name="absoluteExpiresAt">The instant the session expires.</param>
    public void SetSession(HttpResponse response, string handle, DateTimeOffset absoluteExpiresAt)
    {
        var now = timeProvider.GetUtcNow();
        var cookieOptions = CreateOptions(options.Value.Session.SameSite);
        cookieOptions.MaxAge = absoluteExpiresAt - now;
        response.Cookies.Append(BffDefaults.SessionCookieName, handle, cookieOptions);
    }

    /// <summary>
    /// Deletes the session cookie.
    /// </summary>
    /// <param name="response">The current response.</param>
    public void DeleteSession(HttpResponse response)
    {
        var cookieOptions = CreateOptions(options.Value.Session.SameSite);
        response.Cookies.Delete(BffDefaults.SessionCookieName, cookieOptions);
    }

    /// <summary>
    /// Sets the login cookie, which lives as long as a login attempt.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="secret">The login secret.</param>
    public void SetLogin(HttpResponse response, string secret)
    {
        var cookieOptions = CreateOptions(SameSiteMode.Lax);
        cookieOptions.MaxAge = options.Value.Session.LoginLifetime;
        response.Cookies.Append(BffDefaults.LoginCookieName, secret, cookieOptions);
    }

    /// <summary>
    /// Deletes the login cookie.
    /// </summary>
    /// <param name="response">The current response.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public void DeleteLogin(HttpResponse response)
    {
        var cookieOptions = CreateOptions(SameSiteMode.Lax);
        response.Cookies.Delete(BffDefaults.LoginCookieName, cookieOptions);
    }

    /// <summary>
    /// Reads the session handle of a frontend from the request, or returns <see langword="null"/> when the cookie is
    /// missing or malformed.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="frontend">The frontend that the request belongs to.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public string? ReadSessionHandle(HttpRequest request, BffFrontend frontend) =>
        Read(request, frontend.SessionCookieName);

    /// <summary>
    /// Reads the login secret of a frontend from the request, or returns <see langword="null"/> when the cookie is
    /// missing or malformed.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="frontend">The frontend that the request belongs to.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public string? ReadLoginSecret(HttpRequest request, BffFrontend frontend) =>
        Read(request, frontend.LoginCookieName);

    /// <summary>
    /// Sets the session cookie of a frontend so that it expires with the session.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="frontend">The frontend that the session belongs to.</param>
    /// <param name="handle">The session handle.</param>
    /// <param name="absoluteExpiresAt">The instant the session expires.</param>
    public void SetSession(
        HttpResponse response,
        BffFrontend frontend,
        string handle,
        DateTimeOffset absoluteExpiresAt
    )
    {
        var now = timeProvider.GetUtcNow();
        var cookieOptions = CreateOptions(frontend.Options.Session.SameSite);
        cookieOptions.MaxAge = absoluteExpiresAt - now;
        response.Cookies.Append(frontend.SessionCookieName, handle, cookieOptions);
    }

    /// <summary>
    /// Deletes the session cookie of a frontend.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="frontend">The frontend that the session belongs to.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public void DeleteSession(HttpResponse response, BffFrontend frontend)
    {
        var cookieOptions = CreateOptions(frontend.Options.Session.SameSite);
        response.Cookies.Delete(frontend.SessionCookieName, cookieOptions);
    }

    /// <summary>
    /// Sets the login cookie of a frontend, which lives as long as a login attempt.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="frontend">The frontend that the login belongs to.</param>
    /// <param name="secret">The login secret.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public void SetLogin(HttpResponse response, BffFrontend frontend, string secret)
    {
        var cookieOptions = CreateOptions(SameSiteMode.Lax);
        cookieOptions.MaxAge = frontend.Options.Session.LoginLifetime;
        response.Cookies.Append(frontend.LoginCookieName, secret, cookieOptions);
    }

    /// <summary>
    /// Deletes the login cookie of a frontend.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="frontend">The frontend that the login belongs to.</param>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the writers."
    )]
    public void DeleteLogin(HttpResponse response, BffFrontend frontend)
    {
        var cookieOptions = CreateOptions(SameSiteMode.Lax);
        response.Cookies.Delete(frontend.LoginCookieName, cookieOptions);
    }

    private static string? Read(HttpRequest request, string name)
    {
        var found = request.Cookies.TryGetValue(name, out var value);
        return found && value is { Length: ValueLength } ? value : null;
    }

    private static CookieOptions CreateOptions(SameSiteMode sameSite) =>
        new()
        {
            Secure = true,
            HttpOnly = true,
            Path = "/",
            SameSite = sameSite,
        };
}
