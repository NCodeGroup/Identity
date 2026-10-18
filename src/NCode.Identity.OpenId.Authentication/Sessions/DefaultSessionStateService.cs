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

using Microsoft.AspNetCore.Http;
using NCode.Identity;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides a default implementation of the <see cref="ISessionStateService"/> abstraction (OpenID Connect Session
/// Management 1.0). The OP browser state is an opaque per-login value stored in a non-<c>HttpOnly</c> cookie (so the
/// <c>check_session_iframe</c> script can read it) that is deleted on logout; the <c>session_state</c> is a salted
/// <c>SHA-256</c> digest of the client id, the client's origin, and the browser state.
/// </summary>
internal class DefaultSessionStateService(ICryptoService cryptoService) : ISessionStateService
{
    // The OpenID Provider browser-state cookie (the "User Agent State" in OpenID Connect Session Management 1.0).
    internal const string BrowserStateCookieName = "opbs";

    private const int SaltByteLength = 16;

    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public string CreateSessionState(
        OpenIdContext openIdContext,
        string clientId,
        Uri clientRedirectUri
    )
    {
        var browserState = EnsureBrowserState(openIdContext);
        var origin = clientRedirectUri.GetLeftPart(UriPartial.Authority);
        var salt = CryptoService.GenerateUrlSafeKey(SaltByteLength);

        var digest = CryptoService.HashValue(
            $"{clientId} {origin} {browserState} {salt}",
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64Url
        );

        return $"{digest}.{salt}";
    }

    /// <inheritdoc />
    public void ClearBrowserState(OpenIdContext openIdContext) =>
        openIdContext.Http.Response.Cookies.Delete(BrowserStateCookieName, CreateCookieOptions());

    private string EnsureBrowserState(OpenIdContext openIdContext)
    {
        var httpContext = openIdContext.Http;

        if (
            httpContext.Request.Cookies.TryGetValue(BrowserStateCookieName, out var existing)
            && !string.IsNullOrEmpty(existing)
        )
        {
            return existing;
        }

        var browserState = CryptoService.GenerateUrlSafeKey();
        httpContext.Response.Cookies.Append(
            BrowserStateCookieName,
            browserState,
            CreateCookieOptions()
        );
        return browserState;
    }

    // The check_session_iframe is framed by the relying party (a cross-site context), so the cookie must be readable
    // by script (not HttpOnly) and sent in a third-party context (SameSite=None; Secure).
    private static CookieOptions CreateCookieOptions() =>
        new()
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            IsEssential = true,
        };
}
