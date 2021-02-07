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

using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Playground.Logic
{
    public interface IJwtDecoder
    {
        string DecodeJwt(string jwt, string issuer, string audience, ISecurityKeyCollection securityKeys);
    }

    internal class JwtSecurityTokenDecoder : IJwtDecoder
    {
        private readonly ILogger<JwtSecurityTokenDecoder> _logger;

        public JwtSecurityTokenDecoder(ILogger<JwtSecurityTokenDecoder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
