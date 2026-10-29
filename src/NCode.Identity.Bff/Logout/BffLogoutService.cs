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
/// Ends a BFF session on behalf of the user: revokes the refresh token, removes the session and forgets the cookie.
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
    /// <param name="cancellationToken">The token to cancel the revocation request.</param>
    public virtual async ValueTask EndSessionAsync(
        HttpContext httpContext,
        BffSessionState state,
        string reason,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(state);

        if (state.Payload.RefreshToken is { Length: > 0 } refreshToken)
            await RevokeAsync(refreshToken, cancellationToken);

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
    }

    private async ValueTask RevokeAsync(string refreshToken, CancellationToken cancellationToken)
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
                cancellationToken
            );
            if (error is not null)
                logger.RefreshTokenRevocationFailed(error.Error, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.RefreshTokenRevocationFailed(exception.GetType().Name, exception);
        }
    }
}
