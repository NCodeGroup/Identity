// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains the media types used when rendering an OpenID response. A response formatter
    /// (<c>IOpenIdResponseFormatter</c>) declares the media types it emits using these values.
    /// </summary>
    public static class MediaTypes
    {
        /// <summary>
        /// The <c>application/json</c> media type — the default OAuth 2.0 / OpenID Connect response representation
        /// (<see href="https://datatracker.ietf.org/doc/html/rfc6749#section-5.1">RFC 6749 §5.1</see> requires token and
        /// error responses be <c>application/json</c>).
        /// </summary>
        public const string Json = "application/json";

        /// <summary>
        /// The <c>text/html</c> media type — an interactive (browser) representation, for example the non-redirectable
        /// authorization error page.
        /// </summary>
        public const string Html = "text/html";

        /// <summary>
        /// Contains the <c>application/x-www-form-urlencoded</c> content type used by <c>OAuth</c> and <c>OpenID Connect</c> requests.
        /// </summary>
        public const string FormUrlEncoded = "application/x-www-form-urlencoded";

        /// <summary>
        /// The <c>application/jwk-set+json</c> media type of a JSON Web Key Set
        /// (<see href="https://datatracker.ietf.org/doc/html/rfc7517#section-8.5.1">RFC 7517 §8.5.1</see>).
        /// </summary>
        public const string JwkSet = "application/jwk-set+json";

        /// <summary>
        /// Contains the <c>application/jwt</c> media type of a signed or encrypted JWT response, such as a UserInfo
        /// response (OpenID Connect Core 1.0 §5.3.2).
        /// </summary>
        public const string Jwt = "application/jwt";
    }
}
