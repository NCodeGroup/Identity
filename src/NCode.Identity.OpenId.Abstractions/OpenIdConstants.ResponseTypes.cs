#region Copyright Preamble
//
//    Copyright @ 2023 NCode Group
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
    /// Contains constants for the values of the <c>response_type</c> parameter.
    /// </summary>
    /// <remarks>
    /// A <c>response_type</c> value is a space-delimited list of response-type names whose order does not matter
    /// (RFC 6749 §3.1.1). <see cref="Code"/>, <see cref="IdToken"/>, and <see cref="Token"/> are the names; the
    /// combinations are the multiple-valued response types defined by OAuth 2.0 Multiple Response Type Encoding
    /// Practices, each written in canonical form — its names in ordinal order, separated by a single space — which is
    /// the form the server stores, advertises, and compares.
    /// </remarks>
    public static class ResponseTypes
    {
        /// <summary>
        /// Contains the <c>response_type</c> value <c>code</c>, which requests an authorization code (the
        /// authorization code flow).
        /// </summary>
        public const string Code = "code";

        /// <summary>
        /// Contains the <c>response_type</c> value <c>id_token</c>, which requests an ID token from the authorization
        /// endpoint (the OpenID Connect implicit flow).
        /// </summary>
        public const string IdToken = "id_token";

        /// <summary>
        /// Contains the <c>response_type</c> value <c>token</c>, which requests an access token from the
        /// authorization endpoint (the OAuth 2.0 implicit grant).
        /// </summary>
        public const string Token = "token";

        /// <summary>
        /// Contains the <c>response_type</c> value <c>code id_token</c> (an OpenID Connect hybrid flow).
        /// </summary>
        public const string CodeIdToken = "code id_token";

        /// <summary>
        /// Contains the <c>response_type</c> value <c>code token</c> (an OpenID Connect hybrid flow).
        /// </summary>
        public const string CodeToken = "code token";

        /// <summary>
        /// Contains the <c>response_type</c> value <c>id_token token</c> (the OpenID Connect implicit flow).
        /// </summary>
        public const string IdTokenToken = "id_token token";

        /// <summary>
        /// Contains the <c>response_type</c> value <c>code id_token token</c> (an OpenID Connect hybrid flow).
        /// </summary>
        public const string CodeIdTokenToken = "code id_token token";
    }
}
