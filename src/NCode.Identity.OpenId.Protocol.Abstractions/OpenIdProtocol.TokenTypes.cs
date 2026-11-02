// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for various types of <c>OAuth</c> and <c>OpenID Connect</c> tokens.
    /// </summary>
    public static class TokenTypes
    {
        /// <summary>
        /// Contains the <c>Bearer</c> constant value.
        /// </summary>
        public const string Bearer = "Bearer";

        /// <summary>
        /// Contains the <c>DPoP</c> constant value, the OAuth 2.0 access-token type of a DPoP sender-constrained
        /// token (<see href="https://datatracker.ietf.org/doc/html/rfc9449">RFC 9449</see>). A resource server must
        /// receive such a token in an <c>Authorization: DPoP</c> header accompanied by a matching DPoP proof.
        /// </summary>
        public const string DPoP = "DPoP";

        /// <summary>
        /// Contains the <c>id_token</c> constant value.
        /// </summary>
        public const string IdToken = "id_token";

        /// <summary>
        /// Contains the <c>access_token</c> constant value.
        /// </summary>
        public const string AccessToken = "access_token";

        /// <summary>
        /// Contains the <c>refresh_token</c> constant value.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// Contains the <c>N_A</c> constant value, used as the <c>token_type</c> of an OAuth 2.0 Token Exchange
        /// response (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see>) when the issued token is
        /// not an access token (for example, an <c>id_token</c>) and therefore has no applicable <c>token_type</c>.
        /// </summary>
        public const string NotApplicable = "N_A";
    }
}
