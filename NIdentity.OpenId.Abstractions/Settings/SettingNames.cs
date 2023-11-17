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

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> setting names.
/// </summary>
public static class SettingNames
{
    /// <summary>
    /// Contains the name of <c>access_token_encryption_alg_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionAlgValuesSupported = "access_token_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionEncValuesSupported = "access_token_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_encryption_required</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionRequired = "access_token_encryption_required";

    /// <summary>
    /// Contains the name of <c>access_token_encryption_zip_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionZipValuesSupported = "access_token_encryption_zip_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_lifetime</c> setting.
    /// </summary>
    public const string AccessTokenLifetime = "access_token_lifetime";

    /// <summary>
    /// Contains the name of <c>access_token_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenSigningAlgValuesSupported = "access_token_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_type</c> setting.
    /// </summary>
    public const string AccessTokenType = "access_token_type";

    /// <summary>
    /// Contains the name of <c>acr_values_supported</c> setting.
    /// </summary>
    public const string AcrValuesSupported = "acr_values_supported";

    /// <summary>
    /// Contains the name of <c>allow_loopback_redirect</c> setting.
    /// </summary>
    public const string AllowLoopbackRedirect = "allow_loopback_redirect";

    /// <summary>
    /// Contains the name of <c>allow_plain_code_challenge_method</c> setting.
    /// </summary>
    public const string AllowPlainCodeChallengeMethod = "allow_plain_code_challenge_method";

    /// <summary>
    /// Contains the name of <c>allow_unsafe_token_response</c> setting.
    /// </summary>
    public const string AllowUnsafeTokenResponse = "allow_unsafe_token_response";

    /// <summary>
    /// Contains the name of <c>authorization_code_lifetime</c> setting.
    /// </summary>
    public const string AuthorizationCodeLifetime = "authorization_code_lifetime";

    /// <summary>
    /// Contains the name of <c>authorization_sign_in_scheme</c> setting.
    /// </summary>
    public const string AuthorizationSignInScheme = "authorization_sign_in_scheme";

    /// <summary>
    /// Contains the name of <c>claims_locales_supported</c> setting.
    /// </summary>
    public const string ClaimsLocalesSupported = "claims_locales_supported";

    /// <summary>
    /// Contains the name of <c>claims_parameter_supported</c> setting.
    /// </summary>
    public const string ClaimsParameterSupported = "claims_parameter_supported";

    /// <summary>
    /// Contains the name of <c>claims_supported</c> setting.
    /// </summary>
    public const string ClaimsSupported = "claims_supported";

    /// <summary>
    /// Contains the name of <c>claim_types_supported</c> setting.
    /// </summary>
    public const string ClaimTypesSupported = "claim_types_supported";

    /// <summary>
    /// Contains the name of <c>clock_skew</c> setting.
    /// </summary>
    public const string ClockSkew = "clock_skew";

    /// <summary>
    /// Contains the name of <c>display_values_supported</c> setting.
    /// </summary>
    public const string DisplayValuesSupported = "display_values_supported";

    /// <summary>
    /// Contains the name of <c>grant_types_supported</c> setting.
    /// </summary>
    public const string GrantTypesSupported = "grant_types_supported";

    /// <summary>
    /// Contains the name of <c>id_token_encryption_alg_values_supported</c> setting.
    /// </summary>
    public const string IdTokenEncryptionAlgValuesSupported = "id_token_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>id_token_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string IdTokenEncryptionEncValuesSupported = "id_token_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>id_token_encryption_required</c> setting.
    /// </summary>
    public const string IdTokenEncryptionRequired = "id_token_encryption_required";

    /// <summary>
    /// Contains the name of <c>id_token_encryption_zip_values_supported</c> setting.
    /// </summary>
    public const string IdTokenEncryptionZipValuesSupported = "id_token_encryption_zip_values_supported";

    /// <summary>
    /// Contains the name of <c>id_token_lifetime</c> setting.
    /// </summary>
    public const string IdTokenLifetime = "id_token_lifetime";

    /// <summary>
    /// Contains the name of <c>id_token_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string IdTokenSigningAlgValuesSupported = "id_token_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>prompt_values_supported</c> setting.
    /// </summary>
    public const string PromptValuesSupported = "prompt_values_supported";

    /// <summary>
    /// Contains the name of <c>request_object_encryption_alg_values_supported</c> setting.
    /// </summary>
    public const string RequestObjectEncryptionAlgValuesSupported = "request_object_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>request_object_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string RequestObjectEncryptionEncValuesSupported = "request_object_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>request_object_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string RequestObjectSigningAlgValuesSupported = "request_object_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>request_object_expected_audience</c> setting.
    /// </summary>
    public const string RequestObjectExpectedAudience = "request_object_expected_audience";

    /// <summary>
    /// Contains the name of <c>request_parameter_supported</c> setting.
    /// </summary>
    public const string RequestParameterSupported = "request_parameter_supported";

    /// <summary>
    /// Contains the name of <c>request_uri_parameter_supported</c> setting.
    /// </summary>
    public const string RequestUriParameterSupported = "request_uri_parameter_supported";

    /// <summary>
    /// Contains the name of <c>request_uri_require_strict_content_type</c> setting.
    /// </summary>
    public const string RequestUriRequireStrictContentType = "request_uri_require_strict_content_type";

    /// <summary>
    /// Contains the name of <c>request_uri_expected_content_type</c> setting.
    /// </summary>
    public const string RequestUriExpectedContentType = "request_uri_expected_content_type";

    /// <summary>
    /// Contains the name of <c>require_pkce</c> setting.
    /// </summary>
    public const string RequireCodeChallenge = "require_pkce";

    /// <summary>
    /// Contains the name of <c>require_request_uri_registration</c> setting.
    /// </summary>
    public const string RequireRequestUriRegistration = "require_request_uri_registration";

    /// <summary>
    /// Contains the name of <c>response_modes_supported</c> setting.
    /// </summary>
    public const string ResponseModesSupported = "response_modes_supported";

    /// <summary>
    /// Contains the name of <c>response_types_supported</c> setting.
    /// </summary>
    public const string ResponseTypesSupported = "response_types_supported";

    /// <summary>
    /// Contains the name of <c>scopes_supported</c> setting.
    /// </summary>
    public const string ScopesSupported = "scopes_supported";

    /// <summary>
    /// Contains the name of <c>service_documentation</c> setting.
    /// </summary>
    public const string ServiceDocumentation = "service_documentation";

    /// <summary>
    /// Contains the name of <c>subject_types_supported</c> setting.
    /// </summary>
    public const string SubjectTypesSupported = "subject_types_supported";

    /// <summary>
    /// Contains the name of <c>tenant_issuer</c> setting.
    /// </summary>
    public const string TenantIssuer = "tenant_issuer";

    /// <summary>
    /// Contains the name of <c>token_endpoint_auth_methods_supported</c> setting.
    /// </summary>
    public const string TokenEndpointAuthMethodsSupported = "token_endpoint_auth_methods_supported";

    /// <summary>
    /// Contains the name of <c>token_endpoint_auth_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string TokenEndpointAuthSigningAlgValuesSupported = "token_endpoint_auth_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>ui_locales_supported</c> setting.
    /// </summary>
    public const string UiLocalesSupported = "ui_locales_supported";

    /// <summary>
    /// Contains the name of <c>userinfo_encryption_alg_values_supported</c> setting.
    /// </summary>
    public const string UserInfoEncryptionAlgValuesSupported = "userinfo_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>userinfo_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string UserInfoEncryptionEncValuesSupported = "userinfo_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>userinfo_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string UserInfoSigningAlgValuesSupported = "userinfo_signing_alg_values_supported";
}
