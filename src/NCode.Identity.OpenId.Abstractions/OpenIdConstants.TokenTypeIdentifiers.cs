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

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the OAuth 2.0 Token Exchange (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see>)
    /// token type identifier URIs used by the <c>subject_token_type</c>, <c>actor_token_type</c>,
    /// <c>requested_token_type</c>, and <c>issued_token_type</c> parameters. These are distinct from
    /// <see cref="TokenTypes"/>, which carries the HTTP <c>token_type</c> values (<c>Bearer</c>/<c>DPoP</c>).
    /// </summary>
    public static class TokenTypeIdentifiers
    {
        /// <summary>
        /// Contains the constant value 'urn:ietf:params:oauth:token-type:access_token' (RFC 8693 §3), indicating the
        /// token is an OAuth 2.0 access token.
        /// </summary>
        public const string AccessToken = "urn:ietf:params:oauth:token-type:access_token";

        /// <summary>
        /// Contains the constant value 'urn:ietf:params:oauth:token-type:refresh_token' (RFC 8693 §3), indicating the
        /// token is an OAuth 2.0 refresh token.
        /// </summary>
        public const string RefreshToken = "urn:ietf:params:oauth:token-type:refresh_token";

        /// <summary>
        /// Contains the constant value 'urn:ietf:params:oauth:token-type:id_token' (RFC 8693 §3), indicating the token
        /// is an OpenID Connect ID token.
        /// </summary>
        public const string IdToken = "urn:ietf:params:oauth:token-type:id_token";

        /// <summary>
        /// Contains the constant value 'urn:ietf:params:oauth:token-type:jwt' (RFC 8693 §3), indicating the token is a
        /// JSON Web Token (<see href="https://datatracker.ietf.org/doc/html/rfc7519">RFC 7519</see>) that is not
        /// otherwise described by a more specific token type identifier.
        /// </summary>
        public const string Jwt = "urn:ietf:params:oauth:token-type:jwt";
    }
}
