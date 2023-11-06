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

namespace NIdentity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> parameters.
    /// </summary>
    public static class Parameters
    {
        /// <summary>
        /// Contains the name of <c>access_token</c> parameter.
        /// </summary>
        public const string AccessToken = "access_token";

        /// <summary>
        /// Contains the name of <c>at_hash</c> parameter.
        /// </summary>
        public const string AccessTokenHash = "at_hash";

        /// <summary>
        /// Contains the name of <c>acr_values</c> parameter.
        /// </summary>
        public const string AcrValues = "acr_values";

        /// <summary>
        /// Contains the name of <c>acr_values_supported</c> parameter.
        /// </summary>
        public const string AcrValuesSupported = "acr_values_supported";

        /// <summary>
        /// Contains the name of <c>assertion</c> parameter.
        /// </summary>
        public const string Assertion = "assertion";

        /// <summary>
        /// Contains the name of <c>audience</c> parameter.
        /// </summary>
        public const string Audience = "audience";

        /// <summary>
        /// Contains the name of <c>auth_time</c> parameter.
        /// </summary>
        public const string AuthTime = "auth_time";

        /// <summary>
        /// Contains the name of <c>claims</c> parameter.
        /// </summary>
        public const string Claims = "claims";

        /// <summary>
        /// Contains the name of <c>claims_locales</c> parameter.
        /// </summary>
        public const string ClaimsLocales = "claims_locales";

        /// <summary>
        /// Contains the name of <c>claims_locales_supported</c> parameter.
        /// </summary>
        public const string ClaimsLocalesSupported = "claims_locales_supported";

        /// <summary>
        /// Contains the name of <c>claims_parameter_supported</c> parameter.
        /// </summary>
        public const string ClaimsParameterSupported = "claims_parameter_supported";

        /// <summary>
        /// Contains the name of <c>claims_supported</c> parameter.
        /// </summary>
        public const string ClaimsSupported = "claims_supported";

        /// <summary>
        /// Contains the name of <c>claim_types_supported</c> parameter.
        /// </summary>
        public const string ClaimTypesSupported = "claim_types_supported";

        /// <summary>
        /// Contains the name of <c>client_assertion</c> parameter.
        /// </summary>
        public const string ClientAssertion = "client_assertion";

        /// <summary>
        /// Contains the name of <c>client_assertion_type</c> parameter.
        /// </summary>
        public const string ClientAssertionType = "client_assertion_type";

        /// <summary>
        /// Contains the name of <c>client_id</c> parameter.
        /// </summary>
        public const string ClientId = "client_id";

        /// <summary>
        /// Contains the name of <c>client_secret</c> parameter.
        /// </summary>
        public const string ClientSecret = "client_secret";

        /// <summary>
        /// Contains the name of <c>code</c> parameter.
        /// </summary>
        public const string Code = "code";

        /// <summary>
        /// Contains the name of <c>c_hash</c> parameter.
        /// </summary>
        public const string CodeHash = "c_hash";

        /// <summary>
        /// Contains the name of <c>code_challenge</c> parameter.
        /// </summary>
        public const string CodeChallenge = "code_challenge";

        /// <summary>
        /// Contains the name of <c>code_challenge_method</c> parameter.
        /// </summary>
        public const string CodeChallengeMethod = "code_challenge_method";

        /// <summary>
        /// Contains the name of <c>code_verifier</c> parameter.
        /// </summary>
        public const string CodeVerifier = "code_verifier";

        /// <summary>
        /// Contains the name of <c>device_code</c> parameter.
        /// </summary>
        public const string DeviceCode = "device_code";

        /// <summary>
        /// Contains the name of <c>display</c> parameter.
        /// </summary>
        public const string Display = "display";

        /// <summary>
        /// Contains the name of <c>display_values_supported</c> parameter.
        /// </summary>
        public const string DisplayValuesSupported = "display_values_supported";

        /// <summary>
        /// Contains the name of <c>error</c> parameter.
        /// </summary>
        public const string ErrorCode = "error";

        /// <summary>
        /// Contains the name of <c>error_description</c> parameter.
        /// </summary>
        public const string ErrorDescription = "error_description";

        /// <summary>
        /// Contains the name of <c>error_uri</c> parameter.
        /// </summary>
        public const string ErrorUri = "error_uri";

        /// <summary>
        /// Contains the name of <c>essential</c> parameter.
        /// </summary>
        public const string Essential = "essential";

        /// <summary>
        /// Contains the name of <c>exp</c> parameter.
        /// </summary>
        public const string Expiration = "exp";

        /// <summary>
        /// Contains the name of <c>expires_in</c> parameter.
        /// </summary>
        public const string ExpiresIn = "expires_in";

        /// <summary>
        /// Contains the name of <c>grant_type</c> parameter.
        /// </summary>
        public const string GrantType = "grant_type";

        /// <summary>
        /// Contains the name of <c>grant_types_supported</c> parameter.
        /// </summary>
        public const string GrantTypesSupported = "grant_types_supported";

        /// <summary>
        /// Contains the name of <c>identity_provider</c> parameter.
        /// </summary>
        public const string IdentityProvider = "identity_provider";

        /// <summary>
        /// Contains the name of <c>id_token</c> parameter.
        /// </summary>
        public const string IdToken = "id_token";

        /// <summary>
        /// Contains the name of <c>id_token_encryption_alg_values_supported</c> parameter.
        /// </summary>
        public const string IdTokenEncryptionAlgValuesSupported = "id_token_encryption_alg_values_supported";

        /// <summary>
        /// Contains the name of <c>id_token_encryption_enc_values_supported</c> parameter.
        /// </summary>
        public const string IdTokenEncryptionEncValuesSupported = "id_token_encryption_enc_values_supported";

        /// <summary>
        /// Contains the name of <c>id_token_hint</c> parameter.
        /// </summary>
        public const string IdTokenHint = "id_token_hint";

        /// <summary>
        /// Contains the name of <c>id_token_signing_alg_values_supported</c> parameter.
        /// </summary>
        public const string IdTokenSigningAlgValuesSupported = "id_token_signing_alg_values_supported";

        /// <summary>
        /// Contains the name of <c>iat</c> parameter.
        /// </summary>
        public const string IssuedAt = "iat";

        /// <summary>
        /// Contains the name of <c>iss</c> parameter.
        /// </summary>
        public const string IssuerLong = "issuer";

        /// <summary>
        /// Contains the name of <c>iss</c> parameter.
        /// </summary>
        public const string IssuerShort = "iss";

        /// <summary>
        /// Contains the name of <c>login_hint</c> parameter.
        /// </summary>
        public const string LoginHint = "login_hint";

        /// <summary>
        /// Contains the name of <c>max_age</c> parameter.
        /// </summary>
        public const string MaxAge = "max_age";

        /// <summary>
        /// Contains the name of <c>nonce</c> parameter.
        /// </summary>
        public const string Nonce = "nonce";

        /// <summary>
        /// Contains the name of <c>password</c> parameter.
        /// </summary>
        public const string Password = "password";

        /// <summary>
        /// Contains the name of <c>post_logout_redirect_uri</c> parameter.
        /// </summary>
        public const string PostLogoutRedirectUri = "post_logout_redirect_uri";

        /// <summary>
        /// Contains the name of <c>prompt</c> parameter.
        /// </summary>
        public const string Prompt = "prompt";

        /// <summary>
        /// Contains the name of <c>prompt_values_supported</c> parameter.
        /// </summary>
        public const string PromptValuesSupported = "prompt_values_supported";

        /// <summary>
        /// Contains the name of <c>redirect_uri</c> parameter.
        /// </summary>
        public const string RedirectUri = "redirect_uri";

        /// <summary>
        /// Contains the name of <c>refresh_token</c> parameter.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// Contains the name of <c>registration</c> parameter.
        /// </summary>
        public const string Registration = "registration";

        /// <summary>
        /// Contains the name of <c>request</c> parameter.
        /// </summary>
        public const string Request = "request";

        /// <summary>
        /// Contains the name of <c>request_id</c> parameter.
        /// </summary>
        public const string RequestId = "request_id";

        /// <summary>
        /// Contains the name of <c>request_object_encryption_alg_values_supported</c> parameter.
        /// </summary>
        public const string RequestObjectEncryptionAlgValuesSupported = "request_object_encryption_alg_values_supported";

        /// <summary>
        /// Contains the name of <c>request_object_encryption_enc_values_supported</c> parameter.
        /// </summary>
        public const string RequestObjectEncryptionEncValuesSupported = "request_object_encryption_enc_values_supported";

        /// <summary>
        /// Contains the name of <c>request_object_signing_alg_values_supported</c> parameter.
        /// </summary>
        public const string RequestObjectSigningAlgValuesSupported = "request_object_signing_alg_values_supported";

        /// <summary>
        /// Contains the name of <c>request_parameter_supported</c> parameter.
        /// </summary>
        public const string RequestParameterSupported = "request_parameter_supported";

        /// <summary>
        /// Contains the name of <c>require_request_uri_registration</c> parameter.
        /// </summary>
        public const string RequireRequestUriRegistration = "require_request_uri_registration";

        /// <summary>
        /// Contains the name of <c>request_uri</c> parameter.
        /// </summary>
        public const string RequestUri = "request_uri";

        /// <summary>
        /// Contains the name of <c>request_uri_parameter_supported</c> parameter.
        /// </summary>
        public const string RequestUriParameterSupported = "request_uri_parameter_supported";

        /// <summary>
        /// Contains the name of <c>resource</c> parameter.
        /// </summary>
        public const string Resource = "resource";

        /// <summary>
        /// Contains the name of <c>response_mode</c> parameter.
        /// </summary>
        public const string ResponseMode = "response_mode";

        /// <summary>
        /// Contains the name of <c>response_modes_supported</c> parameter.
        /// </summary>
        public const string ResponseModesSupported = "response_modes_supported";

        /// <summary>
        /// Contains the name of <c>response_type</c> parameter.
        /// </summary>
        public const string ResponseType = "response_type";

        /// <summary>
        /// Contains the name of <c>response_types_supported</c> parameter.
        /// </summary>
        public const string ResponseTypesSupported = "response_types_supported";

        /// <summary>
        /// Contains the name of <c>scope</c> parameter.
        /// </summary>
        public const string Scope = "scope";

        /// <summary>
        /// Contains the name of <c>scopes_supported</c> parameter.
        /// </summary>
        public const string ScopesSupported = "scopes_supported";

        /// <summary>
        /// Contains the name of <c>service_documentation</c> parameter.
        /// </summary>
        public const string ServiceDocumentation = "service_documentation";

        /// <summary>
        /// Contains the name of <c>state</c> parameter.
        /// </summary>
        public const string State = "state";

        /// <summary>
        /// Contains the name of <c>s_hash</c> parameter.
        /// </summary>
        public const string StateHash = "s_hash";

        /// <summary>
        /// Contains the name of <c>sub</c> parameter.
        /// </summary>
        public const string Subject = "sub";

        /// <summary>
        /// Contains the name of <c>subject_types_supported</c> parameter.
        /// </summary>
        public const string SubjectTypesSupported = "subject_types_supported";

        /// <summary>
        /// Contains the name of <c>token</c> parameter.
        /// </summary>
        public const string Token = "token";

        /// <summary>
        /// Contains the name of <c>token_endpoint_auth_methods_supported</c> parameter.
        /// </summary>
        public const string TokenEndpointAuthMethodsSupported = "token_endpoint_auth_methods_supported";

        /// <summary>
        /// Contains the name of <c>token_endpoint_auth_signing_alg_values_supported</c> parameter.
        /// </summary>
        public const string TokenEndpointAuthSigningAlgValuesSupported = "token_endpoint_auth_signing_alg_values_supported";

        /// <summary>
        /// Contains the name of <c>token_type</c> parameter.
        /// </summary>
        public const string TokenType = "token_type";

        /// <summary>
        /// Contains the name of <c>token_type_hint</c> parameter.
        /// </summary>
        public const string TokenTypeHint = "token_type_hint";

        /// <summary>
        /// Contains the name of <c>ui_locales</c> parameter.
        /// </summary>
        public const string UiLocales = "ui_locales";

        /// <summary>
        /// Contains the name of <c>ui_locales_supported</c> parameter.
        /// </summary>
        public const string UiLocalesSupported = "ui_locales_supported";

        /// <summary>
        /// Contains the name of <c>user_code</c> parameter.
        /// </summary>
        public const string UserCode = "user_code";

        /// <summary>
        /// Contains the name of <c>userinfo</c> parameter.
        /// </summary>
        public const string UserInfo = "userinfo";

        /// <summary>
        /// Contains the name of <c>userinfo_encryption_alg_values_supported</c> parameter.
        /// </summary>
        public const string UserInfoEncryptionAlgValuesSupported = "userinfo_encryption_alg_values_supported";

        /// <summary>
        /// Contains the name of <c>userinfo_encryption_enc_values_supported</c> parameter.
        /// </summary>
        public const string UserInfoEncryptionEncValuesSupported = "userinfo_encryption_enc_values_supported";

        /// <summary>
        /// Contains the name of <c>userinfo_signing_alg_values_supported</c> parameter.
        /// </summary>
        public const string UserInfoSigningAlgValuesSupported = "userinfo_signing_alg_values_supported";

        /// <summary>
        /// Contains the name of <c>username</c> parameter.
        /// </summary>
        public const string Username = "username";

        /// <summary>
        /// Contains the name of <c>value</c> parameter.
        /// </summary>
        public const string Value = "value";

        /// <summary>
        /// Contains the name of <c>values</c> parameter.
        /// </summary>
        public const string Values = "values";
    }
}
