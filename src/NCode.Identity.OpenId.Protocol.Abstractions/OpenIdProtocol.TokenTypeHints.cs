// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for the known values of the <c>token_type_hint</c> parameter
    /// (<see href="https://datatracker.ietf.org/doc/html/rfc7009#section-2.1">RFC 7009 §2.1</see>). These are the
    /// known values, not all of them: the parameter is an open vocabulary.
    /// </summary>
    public static class TokenTypeHints
    {
        /// <summary>
        /// Contains the <c>token_type_hint</c> parameter value for an access token.
        /// </summary>
        public const string AccessToken = "access_token";

        /// <summary>
        /// Contains the <c>token_type_hint</c> parameter value for a refresh token.
        /// </summary>
        public const string RefreshToken = "refresh_token";
    }
}
