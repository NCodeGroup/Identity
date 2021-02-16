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

namespace NIdentity.OpenId
{
    public static partial class OpenIdConstants
    {
        /// <summary>
        /// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> error codes.
        /// </summary>
        public static class ErrorCodes
        {
            // OAuth 2.0 Authorization Error Response
            // https://tools.ietf.org/html/rfc6749#section-4.1.2.1

            /// <summary>
            /// Contains the error code for <c>invalid_request</c>.
            /// </summary>
            public const string InvalidRequest = "invalid_request";

            /// <summary>
            /// Contains the error code for <c>unauthorized_client</c>.
            /// </summary>
            public const string UnauthorizedClient = "unauthorized_client";

            /// <summary>
            /// Contains the error code for <c>access_denied</c>.
            /// </summary>
            public const string AccessDenied = "access_denied";

            /// <summary>
            /// Contains the error code for <c>unsupported_response_type</c>.
            /// </summary>
            public const string UnsupportedResponseType = "unsupported_response_type";

            /// <summary>
            /// Contains the error code for <c>invalid_scope</c>.
            /// </summary>
            public const string InvalidScope = "invalid_scope";

            /// <summary>
            /// Contains the error code for <c>server_error</c>.
            /// </summary>
            public const string ServerError = "server_error";

            /// <summary>
            /// Contains the error code for <c>temporarily_unavailable</c>.
            /// </summary>
            public const string TemporarilyUnavailable = "temporarily_unavailable";

            // https://openid.net/specs/openid-connect-core-1_0.html#AuthError

            /// <summary>
            /// Contains the error code for <c>interaction_required</c>.
            /// </summary>
            public const string InteractionRequired = "interaction_required";

            /// <summary>
            /// Contains the error code for <c>login_required</c>.
            /// </summary>
            public const string LoginRequired = "login_required";

            /// <summary>
            /// Contains the error code for <c>account_selection_required</c>.
            /// </summary>
            public const string AccountSelectionRequired = "account_selection_required";

            /// <summary>
            /// Contains the error code for <c>consent_required</c>.
            /// </summary>
            public const string ConsentRequired = "consent_required";

            /// <summary>
            /// Contains the error code for <c>invalid_request_uri</c>.
            /// </summary>
            public const string InvalidRequestUri = "invalid_request_uri";

            /// <summary>
            /// Contains the error code for <c>invalid_request_object</c>.
            /// </summary>
            public const string InvalidRequestJwt = "invalid_request_object";

            /// <summary>
            /// Contains the error code for <c>request_not_supported</c>.
            /// </summary>
            public const string RequestNotSupported = "request_not_supported";

            /// <summary>
            /// Contains the error code for <c>request_uri_not_supported</c>.
            /// </summary>
            public const string RequestUriNotSupported = "request_uri_not_supported";

            /// <summary>
            /// Contains the error code for <c>registration_not_supported</c>.
            /// </summary>
            public const string RegistrationNotSupported = "registration_not_supported";
        }
    }
}
