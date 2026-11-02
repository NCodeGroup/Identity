// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for possible values of the <c>response_mode</c> parameter.
    /// </summary>
    public static class ResponseModes
    {
        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>query</c>.
        /// </summary>
        public const string Query = "query";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>fragment</c>.
        /// </summary>
        public const string Fragment = "fragment";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>form_post</c>.
        /// </summary>
        public const string FormPost = "form_post";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>query.jwt</c>: the response parameters travel as one
        /// signed (and optionally encrypted) JWT in the <c>response</c> query parameter (JARM §2.3.1).
        /// </summary>
        public const string QueryJwt = "query.jwt";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>fragment.jwt</c>: the response JWT travels in the
        /// <c>response</c> fragment parameter (JARM §2.3.2).
        /// </summary>
        public const string FragmentJwt = "fragment.jwt";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>form_post.jwt</c>: the response JWT travels in the
        /// <c>response</c> form field (JARM §2.3.3).
        /// </summary>
        public const string FormPostJwt = "form_post.jwt";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>jwt</c>: the default response mode of the response
        /// type, as a JWT — <c>query.jwt</c>, or <c>fragment.jwt</c> for a response type that returns a token (JARM
        /// §2.3.4).
        /// </summary>
        public const string Jwt = "jwt";
    }
}
