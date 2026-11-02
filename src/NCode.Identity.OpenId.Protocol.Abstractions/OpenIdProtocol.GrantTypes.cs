// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constant values for various <c>OAuth</c> and <c>OpenID Connect</c> grant types.
    /// </summary>
    public static class GrantTypes
    {
        /// <summary>
        /// Contains the constant value 'authorization_code'.
        /// </summary>
        public const string AuthorizationCode = "authorization_code";

        /// <summary>
        /// Contains the constant value 'implicit'. A token issued directly by the authorization endpoint is issued
        /// through the implicit grant, including in an OpenID Connect hybrid flow, which uses both this grant and
        /// <see cref="AuthorizationCode"/>.
        /// </summary>
        public const string Implicit = "implicit";

        /// <summary>
        /// Contains the constant value 'password'.
        /// </summary>
        public const string Password = "password";

        /// <summary>
        /// Contains the constant value 'client_credentials'.
        /// </summary>
        public const string ClientCredentials = "client_credentials";

        /// <summary>
        /// Contains the constant value 'refresh_token'.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// Contains the constant value 'urn:ietf:params:oauth:grant-type:device_code'.
        /// </summary>
        public const string DeviceCode = "urn:ietf:params:oauth:grant-type:device_code";

        /// <summary>
        /// Contains the constant value 'urn:openid:params:grant-type:ciba' (OpenID Connect Client-Initiated
        /// Backchannel Authentication).
        /// </summary>
        public const string Ciba = "urn:openid:params:grant-type:ciba";

        /// <summary>
        /// Contains the constant value 'urn:ietf:params:oauth:grant-type:token-exchange'
        /// (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see> OAuth 2.0 Token Exchange).
        /// </summary>
        public const string TokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";
    }
}
