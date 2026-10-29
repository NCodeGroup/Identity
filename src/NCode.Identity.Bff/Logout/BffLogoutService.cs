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
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Logout;

/// <summary>
/// Ends a BFF session on behalf of the user: removes the session, forgets the cookie, then revokes the refresh token.
/// </summary>
internal class BffLogoutService(
    IBffSessionManager manager,
    ITokenRevocationClient revocation,
    BffClientRegistrationProvider registrations,
    BffCookies cookies,
    ILogger<BffLogoutService> logger
)
{
    /// <summary>
    /// Ends the session of the request.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="state">The session to end.</param>
    /// <param name="reason">Why the session ends; see <see cref="BffSessionEndReasons"/>.</param>
    public virtual async ValueTask EndSessionAsync(
        HttpContext httpContext,
        BffSessionState state,
        string reason
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(state);

        await manager.EndAsync(
            httpContext,
            state.StoreKey,
            state.Metadata,
            reason,
            CancellationToken.None
        );
        cookies.DeleteSession(httpContext.Response);

        if (httpContext.Features.Get<BffRequestFeature>() is { } feature)
        {
            feature.IsResolved = true;
            feature.Session = null;
            feature.Principal = null;
        }

        // Revocation follows the removal and nothing cancels it, so a disconnect cannot leave the session behind.
        // See ADR-0293.
        if (state.Payload.RefreshToken is { Length: > 0 } refreshToken)
            await RevokeAsync(refreshToken);
    }

    private async ValueTask RevokeAsync(string refreshToken)
    {
        var request = new TokenRevocationRequest(refreshToken)
        {
            TokenTypeHint = OpenIdProtocol.TokenTypeHints.RefreshToken,
        };

        try
        {
            var error = await revocation.RevokeTokenAsync(
                registrations.Registration,
                request,
                CancellationToken.None
            );
            if (error is not null)
                logger.RefreshTokenRevocationFailed(error.Error, null);
        }
        catch (Exception exception)
        {
            logger.RefreshTokenRevocationFailed(exception.GetType().Name, exception);
        }
    }
}
