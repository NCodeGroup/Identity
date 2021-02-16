#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Logic
{
    /// <summary>
    /// Provides an abstraction to decode and validate Json Web Tokens (i.e. JWT).
    /// For more information see: http://tools.ietf.org/html/rfc7519 and http://www.rfc-editor.org/info/rfc7515
    /// </summary>
    public interface IJwtDecoder
    {
        /// <summary>
        /// Validates a Json Web Token (JWT) and returns the decoded JSON payload.
        /// </summary>
        /// <param name="jwt">The Json Web Token to decode and validate.</param>
        /// <param name="issuer">A <see cref="string"/> that represents a valid issuer that will be used to check against the token's issuer.</param>
        /// <param name="audience">A <see cref="string"/> that represents a valid audience that will be used to check against the token's audience.</param>
        /// <param name="securityKeys">An <see cref="ISecurityKeyCollection"/> used for signature validation.</param>
        /// <returns>The decoded payload from Json Web Token in JSON format.</returns>
        string DecodeJwt(string jwt, string issuer, string audience, ISecurityKeyCollection securityKeys);
    }

    internal class JwtSecurityTokenDecoder : IJwtDecoder
    {
        public string DecodeJwt(string jwt, string issuer, string audience, ISecurityKeyCollection securityKeys)
        {
            var jwtSecurityTokenHandler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidateIssuer = true,

                ValidAudience = audience,
                ValidateAudience = true,

                IssuerSigningKeys = securityKeys,
                ValidateIssuerSigningKey = true,

                RequireSignedTokens = true,
                RequireExpirationTime = true
            };

            jwtSecurityTokenHandler.ValidateToken(jwt, tokenValidationParameters, out var validatedToken);

            var jwtSecurityToken = (JwtSecurityToken)validatedToken;
            var json = jwtSecurityToken.RawPayload;

            return json;
        }
    }
}