// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
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
    BffFrontendCatalog frontends,
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

        var frontend = frontends.GetRequired(httpContext);

        await manager.EndAsync(
            httpContext,
            state.StoreKey,
            state.Metadata,
            reason,
            CancellationToken.None
        );
        cookies.DeleteSession(httpContext.Response, frontend);

        if (httpContext.Features.Get<BffRequestFeature>() is { } feature)
        {
            feature.IsResolved = true;
            feature.Session = null;
            feature.Principal = null;
        }

        // Revocation follows the removal and nothing cancels it, so a disconnect cannot leave the session behind.
        // See ADR-1293.
        if (state.Payload.RefreshToken is { Length: > 0 } refreshToken)
            await RevokeAsync(frontend.Registration, refreshToken);
    }

    private async ValueTask RevokeAsync(ClientRegistration registration, string refreshToken)
    {
        var request = new TokenRevocationRequest(refreshToken)
        {
            TokenTypeHint = OpenIdProtocol.TokenTypeHints.RefreshToken,
        };

        try
        {
            var error = await revocation.RevokeTokenAsync(
                registration,
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
