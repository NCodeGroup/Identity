// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Tokens.Models;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;

/// <summary>
/// Provides extension members that put an issued refresh token on an <see cref="ITokenResponse"/>.
/// </summary>
internal static class TokenResponseRefreshTokenExtensions
{
    extension(ITokenResponse response)
    {
        /// <summary>
        /// Sets the refresh token that a token response issues, and announces as <c>refresh_token_expires_in</c> how
        /// long its rotation family lasts from the moment it was issued.
        /// </summary>
        /// <param name="issued">The issued refresh token.</param>
        // See ADR-1297.
        public void SetRefreshToken(IssuedRefreshToken issued) =>
            response.SetRefreshToken(
                issued.Token.TokenValue,
                issued.LineageExpiresWhen,
                issued.Token.TokenLifetime.StartTime
            );

        /// <summary>
        /// Sets the refresh token that a token response returns, and announces as <c>refresh_token_expires_in</c> how
        /// long its rotation family lasts from <paramref name="utcNow"/>.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="lineageExpiresWhen">When the token's rotation family expires.</param>
        /// <param name="utcNow">The current time.</param>
        // See ADR-1297.
        public void SetRefreshToken(
            string refreshToken,
            DateTimeOffset lineageExpiresWhen,
            DateTimeOffset utcNow
        )
        {
            ArgumentNullException.ThrowIfNull(response);

            var remaining = lineageExpiresWhen - utcNow;
            response.RefreshToken = refreshToken;
            response.RefreshTokenExpiresIn = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
