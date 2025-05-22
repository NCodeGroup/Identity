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

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Contains constants for known <see cref="SettingKey{TValue}"/> instances.
/// </summary>
[PublicAPI]
public static class OpenIdSettingKeys
{
    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'access_token_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> AccessTokenEncryptionAlgValuesSupported =>
        new(OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'access_token_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> AccessTokenEncryptionEncValuesSupported =>
        new(OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'access_token_encryption_required' setting.
    /// </summary>
    public static SettingKey<bool> AccessTokenEncryptionRequired =>
        new(OpenIdSettingNames.AccessTokenEncryptionRequired);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'access_token_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> AccessTokenEncryptionZipValuesSupported =>
        new(OpenIdSettingNames.AccessTokenEncryptionZipValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'access_token_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> AccessTokenLifetime =>
        new(OpenIdSettingNames.AccessTokenLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'access_token_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> AccessTokenSigningAlgValuesSupported =>
        new(OpenIdSettingNames.AccessTokenSigningAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'access_token_type' setting.
    /// </summary>
    public static SettingKey<string> AccessTokenType =>
        new(OpenIdSettingNames.AccessTokenType);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'acr_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> AcrValuesSupported =>
        new(OpenIdSettingNames.AcrValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'allowed_identity_providers' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> AllowedIdentityProviders =>
        new(OpenIdSettingNames.AllowedIdentityProviders);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'allow_loopback_redirect' setting.
    /// </summary>
    public static SettingKey<bool> AllowLoopbackRedirect =>
        new(OpenIdSettingNames.AllowLoopbackRedirect);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'allow_plain_code_challenge_method' setting.
    /// </summary>
    public static SettingKey<bool> AllowPlainCodeChallengeMethod =>
        new(OpenIdSettingNames.AllowPlainCodeChallengeMethod);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'allow_unsafe_token_response' setting.
    /// </summary>
    public static SettingKey<bool> AllowUnsafeTokenResponse =>
        new(OpenIdSettingNames.AllowUnsafeTokenResponse);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'authorization_code_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> AuthorizationCodeLifetime =>
        new(OpenIdSettingNames.AuthorizationCodeLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'authorization_authenticate_scheme' setting.
    /// </summary>
    public static SettingKey<string> AuthorizationAuthenticateScheme =>
        new(OpenIdSettingNames.AuthorizationAuthenticateScheme);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'authorization_challenge_scheme' setting.
    /// </summary>
    public static SettingKey<string> AuthorizationChallengeScheme =>
        new(OpenIdSettingNames.AuthorizationChallengeScheme);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'claims_locales_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> ClaimsLocalesSupported =>
        new(OpenIdSettingNames.ClaimsLocalesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'claims_parameter_supported' setting.
    /// </summary>
    public static SettingKey<bool> ClaimsParameterSupported =>
        new(OpenIdSettingNames.ClaimsParameterSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'claims_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> ClaimsSupported =>
        new(OpenIdSettingNames.ClaimsSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'claims_supported_is_strict' setting.
    /// </summary>
    public static SettingKey<bool> ClaimsSupportedIsStrict =>
        new(OpenIdSettingNames.ClaimsSupportedIsStrict);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'claim_types_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> ClaimTypesSupported =>
        new(OpenIdSettingNames.ClaimTypesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'clock_skew' setting.
    /// </summary>
    public static SettingKey<TimeSpan> ClockSkew =>
        new(OpenIdSettingNames.ClockSkew);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'continue_authorization_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> ContinueAuthorizationLifetime =>
        new(OpenIdSettingNames.ContinueAuthorizationLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'display_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> DisplayValuesSupported =>
        new(OpenIdSettingNames.DisplayValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'grant_types_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> GrantTypesSupported =>
        new(OpenIdSettingNames.GrantTypesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'id_token_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> IdTokenEncryptionAlgValuesSupported =>
        new(OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'id_token_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> IdTokenEncryptionEncValuesSupported =>
        new(OpenIdSettingNames.IdTokenEncryptionEncValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'id_token_encryption_required' setting.
    /// </summary>
    public static SettingKey<bool> IdTokenEncryptionRequired =>
        new(OpenIdSettingNames.IdTokenEncryptionRequired);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'id_token_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> IdTokenEncryptionZipValuesSupported =>
        new(OpenIdSettingNames.IdTokenEncryptionZipValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'id_token_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> IdTokenLifetime =>
        new(OpenIdSettingNames.IdTokenLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'id_token_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> IdTokenSigningAlgValuesSupported =>
        new(OpenIdSettingNames.IdTokenSigningAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'op_policy_uri' setting.
    /// </summary>
    public static SettingKey<string> OpenIdProviderPolicyUri =>
        new(OpenIdSettingNames.OpenIdProviderPolicyUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'op_tos_uri' setting.
    /// </summary>
    public static SettingKey<string> OpenIdProviderTermsOfServiceUri =>
        new(OpenIdSettingNames.OpenIdProviderTermsOfServiceUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'prompt_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> PromptValuesSupported =>
        new(OpenIdSettingNames.PromptValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'redirect_uris' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> RedirectUris =>
        new(OpenIdSettingNames.RedirectUris);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'refresh_token_expiration_policy' setting.
    /// </summary>
    public static SettingKey<string> RefreshTokenExpirationPolicy =>
        new(OpenIdSettingNames.RefreshTokenExpirationPolicy);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'refresh_token_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> RefreshTokenLifetime =>
        new(OpenIdSettingNames.RefreshTokenLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'refresh_token_rotation_enabled' setting.
    /// </summary>
    public static SettingKey<bool> RefreshTokenRotationEnabled =>
        new(OpenIdSettingNames.RefreshTokenRotationEnabled);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_object_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> RequestObjectEncryptionAlgValuesSupported =>
        new(OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_object_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> RequestObjectEncryptionEncValuesSupported =>
        new(OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_object_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> RequestObjectEncryptionZipValuesSupported =>
        new(OpenIdSettingNames.RequestObjectEncryptionZipValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_object_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> RequestObjectSigningAlgValuesSupported =>
        new(OpenIdSettingNames.RequestObjectSigningAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_object_expected_audience' setting.
    /// </summary>
    public static SettingKey<string> RequestObjectExpectedAudience =>
        new(OpenIdSettingNames.RequestObjectExpectedAudience);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_parameter_supported' setting.
    /// </summary>
    public static SettingKey<bool> RequestParameterSupported =>
        new(OpenIdSettingNames.RequestParameterSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_uri_parameter_supported' setting.
    /// </summary>
    public static SettingKey<bool> RequestUriParameterSupported =>
        new(OpenIdSettingNames.RequestUriParameterSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_uri_require_strict_content_type' setting.
    /// </summary>
    public static SettingKey<bool> RequestUriRequireStrictContentType =>
        new(OpenIdSettingNames.RequestUriRequireStrictContentType);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_uri_expected_content_type' setting.
    /// </summary>
    public static SettingKey<string> RequestUriExpectedContentType =>
        new(OpenIdSettingNames.RequestUriExpectedContentType);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'require_pkce' setting.
    /// </summary>
    public static SettingKey<bool> RequireCodeChallenge =>
        new(OpenIdSettingNames.RequireCodeChallenge);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'require_request_object' setting.
    /// </summary>
    public static SettingKey<bool> RequireRequestObject =>
        new(OpenIdSettingNames.RequireRequestObject);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'require_request_uri_registration' setting.
    /// </summary>
    public static SettingKey<bool> RequireRequestUriRegistration =>
        new(OpenIdSettingNames.RequireRequestUriRegistration);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'response_modes_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> ResponseModesSupported =>
        new(OpenIdSettingNames.ResponseModesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'response_types_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> ResponseTypesSupported =>
        new(OpenIdSettingNames.ResponseTypesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'scopes_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> ScopesSupported =>
        new(OpenIdSettingNames.ScopesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_id_claims_in_access_token' setting.
    /// </summary>
    public static SettingKey<bool> SendIdClaimsInAccessToken =>
        new(OpenIdSettingNames.SendIdClaimsInAccessToken);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'service_documentation' setting.
    /// </summary>
    public static SettingKey<string> ServiceDocumentation =>
        new(OpenIdSettingNames.ServiceDocumentation);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'subject_max_age' setting.
    /// </summary>
    public static SettingKey<TimeSpan> SubjectMaxAge =>
        new(OpenIdSettingNames.SubjectMaxAge);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'subject_type' setting.
    /// </summary>
    public static SettingKey<string> SubjectType =>
        new(OpenIdSettingNames.SubjectType);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'subject_types_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> SubjectTypesSupported =>
        new(OpenIdSettingNames.SubjectTypesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'tenant_issuer' setting.
    /// </summary>
    public static SettingKey<string> TenantIssuer =>
        new(OpenIdSettingNames.TenantIssuer);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'token_endpoint_auth_methods_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> TokenEndpointAuthMethodsSupported =>
        new(OpenIdSettingNames.TokenEndpointAuthMethodsSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'token_endpoint_auth_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> TokenEndpointAuthSigningAlgValuesSupported =>
        new(OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'ui_locales_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> UiLocalesSupported =>
        new(OpenIdSettingNames.UiLocalesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'userinfo_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> UserInfoEncryptionAlgValuesSupported =>
        new(OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'userinfo_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> UserInfoEncryptionEncValuesSupported =>
        new(OpenIdSettingNames.UserInfoEncryptionEncValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'userinfo_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> UserInfoEncryptionZipValuesSupported =>
        new(OpenIdSettingNames.UserInfoEncryptionZipValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'userinfo_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> UserInfoSigningAlgValuesSupported =>
        new(OpenIdSettingNames.UserInfoSigningAlgValuesSupported);
}
