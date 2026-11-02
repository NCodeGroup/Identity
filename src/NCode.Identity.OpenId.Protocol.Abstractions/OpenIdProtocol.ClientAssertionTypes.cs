// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for the <c>client_assertion_type</c> values used with JWT client-authentication assertions.
    /// </summary>
    public static class ClientAssertionTypes
    {
        /// <summary>
        /// The <c>client_assertion_type</c> value that identifies a JWT bearer client-authentication assertion,
        /// as defined by <see href="https://datatracker.ietf.org/doc/html/rfc7523#section-2.2">RFC 7523 §2.2</see>
        /// and OpenID Connect Core §9 (<c>private_key_jwt</c> and <c>client_secret_jwt</c>).
        /// </summary>
        public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    }
}
