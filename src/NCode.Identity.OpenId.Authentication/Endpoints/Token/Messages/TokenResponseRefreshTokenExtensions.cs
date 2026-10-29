#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
        /// long its rotation family lasts from the moment it was issued (ADR-0297).
        /// </summary>
        /// <param name="issued">The issued refresh token.</param>
        public void SetRefreshToken(IssuedRefreshToken issued) =>
            response.SetRefreshToken(
                issued.Token.TokenValue,
                issued.LineageExpiresWhen,
                issued.Token.TokenLifetime.StartTime
            );

        /// <summary>
        /// Sets the refresh token that a token response returns, and announces as <c>refresh_token_expires_in</c> how
        /// long its rotation family lasts from <paramref name="utcNow"/> (ADR-0297).
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="lineageExpiresWhen">When the token's rotation family expires.</param>
        /// <param name="utcNow">The current time.</param>
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
