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

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constant values for the known <c>OpenID Provider</c> and authorization server metadata member names
    /// (OpenID Connect Discovery 1.0, section 3, and RFC 8414, section 2).
    /// </summary>
    public static class MetadataNames
    {
        /// <summary>
        /// Contains the constant value 'issuer'.
        /// </summary>
        public const string Issuer = "issuer";

        /// <summary>
        /// Contains the constant value 'authorization_endpoint'.
        /// </summary>
        public const string AuthorizationEndpoint = "authorization_endpoint";

        /// <summary>
        /// Contains the constant value 'token_endpoint'.
        /// </summary>
        public const string TokenEndpoint = "token_endpoint";

        /// <summary>
        /// Contains the constant value 'userinfo_endpoint'.
        /// </summary>
        public const string UserInfoEndpoint = "userinfo_endpoint";

        /// <summary>
        /// Contains the constant value 'jwks_uri'.
        /// </summary>
        public const string JwksUri = "jwks_uri";

        /// <summary>
        /// Contains the constant value 'end_session_endpoint'.
        /// </summary>
        public const string EndSessionEndpoint = "end_session_endpoint";

        /// <summary>
        /// Contains the constant value 'revocation_endpoint'.
        /// </summary>
        public const string RevocationEndpoint = "revocation_endpoint";

        /// <summary>
        /// Contains the constant value 'introspection_endpoint'.
        /// </summary>
        public const string IntrospectionEndpoint = "introspection_endpoint";

        /// <summary>
        /// Contains the constant value 'device_authorization_endpoint'.
        /// </summary>
        public const string DeviceAuthorizationEndpoint = "device_authorization_endpoint";

        /// <summary>
        /// Contains the constant value 'backchannel_authentication_endpoint'.
        /// </summary>
        public const string BackchannelAuthenticationEndpoint =
            "backchannel_authentication_endpoint";

        /// <summary>
        /// Contains the constant value 'pushed_authorization_request_endpoint'.
        /// </summary>
        public const string PushedAuthorizationRequestEndpoint =
            "pushed_authorization_request_endpoint";

        /// <summary>
        /// Contains the constant value 'check_session_iframe'.
        /// </summary>
        public const string CheckSessionIframe = "check_session_iframe";

        /// <summary>
        /// Contains the constant value 'registration_endpoint'.
        /// </summary>
        public const string RegistrationEndpoint = "registration_endpoint";

        /// <summary>
        /// Contains the constant value 'mtls_endpoint_aliases'.
        /// </summary>
        public const string MtlsEndpointAliases = "mtls_endpoint_aliases";

        /// <summary>
        /// Contains the constant value 'require_pushed_authorization_requests'.
        /// </summary>
        public const string RequirePushedAuthorizationRequests =
            "require_pushed_authorization_requests";

        /// <summary>
        /// Contains the constant value 'authorization_response_iss_parameter_supported'.
        /// </summary>
        public const string AuthorizationResponseIssParameterSupported =
            "authorization_response_iss_parameter_supported";

        /// <summary>
        /// Contains the constant value 'backchannel_logout_supported'.
        /// </summary>
        public const string BackchannelLogoutSupported = "backchannel_logout_supported";

        /// <summary>
        /// Contains the constant value 'backchannel_logout_session_supported'.
        /// </summary>
        public const string BackchannelLogoutSessionSupported =
            "backchannel_logout_session_supported";

        /// <summary>
        /// Contains the constant value 'scopes_supported'.
        /// </summary>
        public const string ScopesSupported = "scopes_supported";

        /// <summary>
        /// Contains the constant value 'response_types_supported'.
        /// </summary>
        public const string ResponseTypesSupported = "response_types_supported";

        /// <summary>
        /// Contains the constant value 'grant_types_supported'.
        /// </summary>
        public const string GrantTypesSupported = "grant_types_supported";

        /// <summary>
        /// Contains the constant value 'code_challenge_methods_supported'.
        /// </summary>
        public const string CodeChallengeMethodsSupported = "code_challenge_methods_supported";

        /// <summary>
        /// Contains the constant value 'token_endpoint_auth_methods_supported'.
        /// </summary>
        public const string TokenEndpointAuthMethodsSupported =
            "token_endpoint_auth_methods_supported";

        /// <summary>
        /// Contains the constant value 'token_endpoint_auth_signing_alg_values_supported'.
        /// </summary>
        public const string TokenEndpointAuthSigningAlgValuesSupported =
            "token_endpoint_auth_signing_alg_values_supported";

        /// <summary>
        /// Contains the constant value 'id_token_signing_alg_values_supported'.
        /// </summary>
        public const string IdTokenSigningAlgValuesSupported =
            "id_token_signing_alg_values_supported";

        /// <summary>
        /// Contains the constant value 'dpop_signing_alg_values_supported'.
        /// </summary>
        public const string DPoPSigningAlgValuesSupported = "dpop_signing_alg_values_supported";
    }
}
