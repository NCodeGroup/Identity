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

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
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
