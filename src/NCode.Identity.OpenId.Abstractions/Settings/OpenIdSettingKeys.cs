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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Contains constants for known <see cref="SettingKey{TValue}"/> instances.
/// </summary>
[PublicAPI]
public static class OpenIdSettingKeys
{
    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'subject_claim_types' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> SubjectClaimTypes =>
        new(OpenIdSettingNames.SubjectClaimTypes);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'principal_issuer_claim' setting.
    /// </summary>
    public static SettingKey<string> PrincipalIssuerClaim =>
        new(OpenIdSettingNames.PrincipalIssuerClaim);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'federated_identity_join_claim' setting.
    /// </summary>
    public static SettingKey<string> FederatedIdentityJoinClaim =>
        new(OpenIdSettingNames.FederatedIdentityJoinClaim);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'federated_identity_verified_claim' setting.
    /// </summary>
    public static SettingKey<string> FederatedIdentityVerifiedClaim =>
        new(OpenIdSettingNames.FederatedIdentityVerifiedClaim);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'federated_identity_require_verified' setting.
    /// </summary>
    public static SettingKey<bool> FederatedIdentityRequireVerified =>
        new(OpenIdSettingNames.FederatedIdentityRequireVerified);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'federated_identity_explicit_only' setting.
    /// </summary>
    public static SettingKey<bool> FederatedIdentityExplicitOnly =>
        new(OpenIdSettingNames.FederatedIdentityExplicitOnly);

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
    public static SettingKey<string> AccessTokenType => new(OpenIdSettingNames.AccessTokenType);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'acr_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> AcrValuesSupported =>
        new(OpenIdSettingNames.AcrValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'default_acr' setting.
    /// </summary>
    public static SettingKey<string> DefaultAcr => new(OpenIdSettingNames.DefaultAcr);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'always_include_auth_time' setting.
    /// </summary>
    public static SettingKey<bool> AlwaysIncludeAuthTime =>
        new(OpenIdSettingNames.AlwaysIncludeAuthTime);

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
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'authorization_code_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> AuthorizationCodeLifetime =>
        new(OpenIdSettingNames.AuthorizationCodeLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'authorization_response_iss_parameter_supported' setting.
    /// </summary>
    public static SettingKey<bool> AuthorizationResponseIssParameterSupported =>
        new(OpenIdSettingNames.AuthorizationResponseIssParameterSupported);

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
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'client_assertion_max_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> ClientAssertionMaxLifetime =>
        new(OpenIdSettingNames.ClientAssertionMaxLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'clock_skew' setting.
    /// </summary>
    public static SettingKey<TimeSpan> ClockSkew => new(OpenIdSettingNames.ClockSkew);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'code_challenge_methods_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> CodeChallengeMethodsSupported =>
        new(OpenIdSettingNames.CodeChallengeMethodsSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'continue_authorization_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> ContinueAuthorizationLifetime =>
        new(OpenIdSettingNames.ContinueAuthorizationLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'device_code_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> DeviceCodeLifetime =>
        new(OpenIdSettingNames.DeviceCodeLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'device_code_polling_interval' setting.
    /// </summary>
    public static SettingKey<TimeSpan> DeviceCodePollingInterval =>
        new(OpenIdSettingNames.DeviceCodePollingInterval);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'user_code_length' setting.
    /// </summary>
    public static SettingKey<int> UserCodeLength => new(OpenIdSettingNames.UserCodeLength);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'backchannel_token_delivery_mode' setting.
    /// </summary>
    public static SettingKey<string> BackchannelTokenDeliveryMode =>
        new(OpenIdSettingNames.BackchannelTokenDeliveryMode);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'client_notification_endpoint' setting.
    /// </summary>
    public static SettingKey<string> ClientNotificationEndpoint =>
        new(OpenIdSettingNames.ClientNotificationEndpoint);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'backchannel_token_delivery_modes_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> BackchannelTokenDeliveryModesSupported =>
        new(OpenIdSettingNames.BackchannelTokenDeliveryModesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'backchannel_user_code_parameter_supported' setting.
    /// </summary>
    public static SettingKey<bool> BackchannelUserCodeParameterSupported =>
        new(OpenIdSettingNames.BackchannelUserCodeParameterSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the
    /// 'backchannel_authentication_request_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<
        IReadOnlyCollection<string>
    > BackchannelAuthenticationRequestSigningAlgValuesSupported =>
        new(OpenIdSettingNames.BackchannelAuthenticationRequestSigningAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the
    /// 'backchannel_authentication_request_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<
        IReadOnlyCollection<string>
    > BackchannelAuthenticationRequestEncryptionAlgValuesSupported =>
        new(OpenIdSettingNames.BackchannelAuthenticationRequestEncryptionAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the
    /// 'backchannel_authentication_request_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingKey<
        IReadOnlyCollection<string>
    > BackchannelAuthenticationRequestEncryptionEncValuesSupported =>
        new(OpenIdSettingNames.BackchannelAuthenticationRequestEncryptionEncValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the
    /// 'backchannel_authentication_request_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingKey<
        IReadOnlyCollection<string>
    > BackchannelAuthenticationRequestEncryptionZipValuesSupported =>
        new(OpenIdSettingNames.BackchannelAuthenticationRequestEncryptionZipValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'ciba_request_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> CibaRequestLifetime =>
        new(OpenIdSettingNames.CibaRequestLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'ciba_polling_interval' setting.
    /// </summary>
    public static SettingKey<TimeSpan> CibaPollingInterval =>
        new(OpenIdSettingNames.CibaPollingInterval);

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
    public static SettingKey<TimeSpan> IdTokenLifetime => new(OpenIdSettingNames.IdTokenLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'id_token_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> IdTokenSigningAlgValuesSupported =>
        new(OpenIdSettingNames.IdTokenSigningAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'jwks_uri' setting.
    /// </summary>
    public static SettingKey<string> JwksUri => new(OpenIdSettingNames.JwksUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'jwks_uri_allowed_hosts' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> JwksUriAllowedHosts =>
        new(OpenIdSettingNames.JwksUriAllowedHosts);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'jwks_uri_fetch_timeout' setting.
    /// </summary>
    public static SettingKey<TimeSpan> JwksUriFetchTimeout =>
        new(OpenIdSettingNames.JwksUriFetchTimeout);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'jwks_uri_max_size_bytes' setting.
    /// </summary>
    public static SettingKey<int> JwksUriMaxSizeBytes =>
        new(OpenIdSettingNames.JwksUriMaxSizeBytes);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'jwks_uri_cache_duration' setting.
    /// </summary>
    public static SettingKey<TimeSpan> JwksUriCacheDuration =>
        new(OpenIdSettingNames.JwksUriCacheDuration);

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
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'post_logout_redirect_uris' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> PostLogoutRedirectUris =>
        new(OpenIdSettingNames.PostLogoutRedirectUris);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'backchannel_logout_uri' setting.
    /// </summary>
    public static SettingKey<string> BackChannelLogoutUri =>
        new(OpenIdSettingNames.BackChannelLogoutUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'backchannel_logout_session_required' setting.
    /// </summary>
    public static SettingKey<bool> BackChannelLogoutSessionRequired =>
        new(OpenIdSettingNames.BackChannelLogoutSessionRequired);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'backchannel_logout_supported' setting.
    /// </summary>
    public static SettingKey<bool> BackChannelLogoutSupported =>
        new(OpenIdSettingNames.BackChannelLogoutSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'backchannel_logout_session_supported' setting.
    /// </summary>
    public static SettingKey<bool> BackChannelLogoutSessionSupported =>
        new(OpenIdSettingNames.BackChannelLogoutSessionSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'frontchannel_logout_uri' setting.
    /// </summary>
    public static SettingKey<string> FrontChannelLogoutUri =>
        new(OpenIdSettingNames.FrontChannelLogoutUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'frontchannel_logout_session_required' setting.
    /// </summary>
    public static SettingKey<bool> FrontChannelLogoutSessionRequired =>
        new(OpenIdSettingNames.FrontChannelLogoutSessionRequired);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'frontchannel_logout_supported' setting.
    /// </summary>
    public static SettingKey<bool> FrontChannelLogoutSupported =>
        new(OpenIdSettingNames.FrontChannelLogoutSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'frontchannel_logout_session_supported' setting.
    /// </summary>
    public static SettingKey<bool> FrontChannelLogoutSessionSupported =>
        new(OpenIdSettingNames.FrontChannelLogoutSessionSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'session_management_supported' setting.
    /// </summary>
    public static SettingKey<bool> SessionManagementSupported =>
        new(OpenIdSettingNames.SessionManagementSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'session_absolute_lifetime' setting.
    /// </summary>
    public static SettingKey<TimeSpan> SessionAbsoluteLifetime =>
        new(OpenIdSettingNames.SessionAbsoluteLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'session_idle_timeout' setting.
    /// </summary>
    public static SettingKey<TimeSpan> SessionIdleTimeout =>
        new(OpenIdSettingNames.SessionIdleTimeout);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'max_concurrent_sessions' setting.
    /// </summary>
    public static SettingKey<int> MaxConcurrentSessions =>
        new(OpenIdSettingNames.MaxConcurrentSessions);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'registration_endpoint_allow_anonymous' setting.
    /// </summary>
    public static SettingKey<bool> RegistrationEndpointAllowAnonymous =>
        new(OpenIdSettingNames.RegistrationEndpointAllowAnonymous);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'refresh_token_expiration_policy' setting.
    /// </summary>
    public static SettingKey<string> RefreshTokenExpirationPolicy =>
        new(OpenIdSettingNames.RefreshTokenExpirationPolicy);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'refresh_token_reuse_policy' setting.
    /// </summary>
    public static SettingKey<string> RefreshTokenReusePolicy =>
        new(OpenIdSettingNames.RefreshTokenReusePolicy);

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
    public static SettingKey<
        IReadOnlyCollection<string>
    > RequestObjectEncryptionAlgValuesSupported =>
        new(OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_object_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingKey<
        IReadOnlyCollection<string>
    > RequestObjectEncryptionEncValuesSupported =>
        new(OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_object_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingKey<
        IReadOnlyCollection<string>
    > RequestObjectEncryptionZipValuesSupported =>
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
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_uri_allowed_hosts' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> RequestUriAllowedHosts =>
        new(OpenIdSettingNames.RequestUriAllowedHosts);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_uri_fetch_timeout' setting.
    /// </summary>
    public static SettingKey<TimeSpan> RequestUriFetchTimeout =>
        new(OpenIdSettingNames.RequestUriFetchTimeout);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'request_uri_max_size_bytes' setting.
    /// </summary>
    public static SettingKey<int> RequestUriMaxSizeBytes =>
        new(OpenIdSettingNames.RequestUriMaxSizeBytes);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'require_consent' setting.
    /// </summary>
    public static SettingKey<bool> RequireConsent => new(OpenIdSettingNames.RequireConsent);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'require_pkce' setting.
    /// </summary>
    public static SettingKey<bool> RequireCodeChallenge =>
        new(OpenIdSettingNames.RequireCodeChallenge);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'pkce_nonce_exemption' setting.
    /// </summary>
    public static SettingKey<bool> PkceNonceExemption => new(OpenIdSettingNames.PkceNonceExemption);

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
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'require_pushed_authorization_requests' setting (RFC 9126).
    /// </summary>
    public static SettingKey<bool> RequirePushedAuthorizationRequests =>
        new(OpenIdSettingNames.RequirePushedAuthorizationRequests);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'pushed_authorization_request_lifetime' setting (RFC 9126).
    /// </summary>
    public static SettingKey<TimeSpan> PushedAuthorizationRequestLifetime =>
        new(OpenIdSettingNames.PushedAuthorizationRequestLifetime);

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
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_id_claims_in_access_token' setting.
    /// </summary>
    public static SettingKey<bool> SendIdClaimsInAccessToken =>
        new(OpenIdSettingNames.SendIdClaimsInAccessToken);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'token_exchange_allow_refresh_token' setting.
    /// </summary>
    public static SettingKey<bool> TokenExchangeAllowRefreshToken =>
        new(OpenIdSettingNames.TokenExchangeAllowRefreshToken);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_profile_metadata_in_access_token' setting.
    /// </summary>
    public static SettingKey<bool> SendProfileMetadataInAccessToken =>
        new(OpenIdSettingNames.SendProfileMetadataInAccessToken);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_profile_metadata_in_id_token' setting.
    /// </summary>
    public static SettingKey<bool> SendProfileMetadataInIdToken =>
        new(OpenIdSettingNames.SendProfileMetadataInIdToken);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_profile_metadata_in_user_info' setting.
    /// </summary>
    public static SettingKey<bool> SendProfileMetadataInUserInfo =>
        new(OpenIdSettingNames.SendProfileMetadataInUserInfo);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_system_metadata_in_access_token' setting.
    /// </summary>
    public static SettingKey<bool> SendSystemMetadataInAccessToken =>
        new(OpenIdSettingNames.SendSystemMetadataInAccessToken);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_system_metadata_in_id_token' setting.
    /// </summary>
    public static SettingKey<bool> SendSystemMetadataInIdToken =>
        new(OpenIdSettingNames.SendSystemMetadataInIdToken);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'send_system_metadata_in_user_info' setting.
    /// </summary>
    public static SettingKey<bool> SendSystemMetadataInUserInfo =>
        new(OpenIdSettingNames.SendSystemMetadataInUserInfo);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'service_documentation' setting.
    /// </summary>
    public static SettingKey<string> ServiceDocumentation =>
        new(OpenIdSettingNames.ServiceDocumentation);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'security_profile' setting.
    /// </summary>
    public static SettingKey<string> SecurityProfile => new(OpenIdSettingNames.SecurityProfile);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'pairwise_salt' setting.
    /// </summary>
    public static SettingKey<string> PairwiseSalt => new(OpenIdSettingNames.PairwiseSalt);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'sector_identifier_uri' setting.
    /// </summary>
    public static SettingKey<string> SectorIdentifierUri =>
        new(OpenIdSettingNames.SectorIdentifierUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'sector_identifier_uri_allowed_hosts' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> SectorIdentifierUriAllowedHosts =>
        new(OpenIdSettingNames.SectorIdentifierUriAllowedHosts);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'sector_identifier_uri_fetch_timeout' setting.
    /// </summary>
    public static SettingKey<TimeSpan> SectorIdentifierUriFetchTimeout =>
        new(OpenIdSettingNames.SectorIdentifierUriFetchTimeout);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'sector_identifier_uri_max_size_bytes' setting.
    /// </summary>
    public static SettingKey<int> SectorIdentifierUriMaxSizeBytes =>
        new(OpenIdSettingNames.SectorIdentifierUriMaxSizeBytes);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'subject_max_age' setting.
    /// </summary>
    public static SettingKey<TimeSpan> SubjectMaxAge => new(OpenIdSettingNames.SubjectMaxAge);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'subject_type' setting.
    /// </summary>
    public static SettingKey<string> SubjectType => new(OpenIdSettingNames.SubjectType);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'subject_types_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> SubjectTypesSupported =>
        new(OpenIdSettingNames.SubjectTypesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'token_endpoint_auth_methods_supported' setting.
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> TokenEndpointAuthMethodsSupported =>
        new(OpenIdSettingNames.TokenEndpointAuthMethodsSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'token_endpoint_auth_method' setting.
    /// </summary>
    public static SettingKey<string> TokenEndpointAuthMethod =>
        new(OpenIdSettingNames.TokenEndpointAuthMethod);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'token_endpoint_auth_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingKey<
        IReadOnlyCollection<string>
    > TokenEndpointAuthSigningAlgValuesSupported =>
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

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'dpop_signing_alg_values_supported' setting (RFC 9449).
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> DPoPSigningAlgValuesSupported =>
        new(OpenIdSettingNames.DPoPSigningAlgValuesSupported);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'dpop_proof_lifetime' setting (RFC 9449).
    /// </summary>
    public static SettingKey<TimeSpan> DPoPProofLifetime =>
        new(OpenIdSettingNames.DPoPProofLifetime);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'dpop_nonce_required' setting (RFC 9449 §8).
    /// </summary>
    public static SettingKey<bool> DPoPNonceRequired => new(OpenIdSettingNames.DPoPNonceRequired);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'require_dpop' setting (RFC 9449).
    /// </summary>
    public static SettingKey<bool> RequireDPoP => new(OpenIdSettingNames.RequireDPoP);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'tls_client_certificate_bound_access_tokens' setting
    /// (RFC 8705 §3.3).
    /// </summary>
    public static SettingKey<bool> TlsClientCertificateBoundAccessTokens =>
        new(OpenIdSettingNames.TlsClientCertificateBoundAccessTokens);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'tls_client_auth_subject_dn' setting (RFC 8705 §2.1.2).
    /// </summary>
    public static SettingKey<string> TlsClientAuthSubjectDn =>
        new(OpenIdSettingNames.TlsClientAuthSubjectDn);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'tls_client_auth_san_dns' setting (RFC 8705 §2.1.2).
    /// </summary>
    public static SettingKey<string> TlsClientAuthSanDns =>
        new(OpenIdSettingNames.TlsClientAuthSanDns);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'tls_client_auth_san_uri' setting (RFC 8705 §2.1.2).
    /// </summary>
    public static SettingKey<string> TlsClientAuthSanUri =>
        new(OpenIdSettingNames.TlsClientAuthSanUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'tls_client_auth_san_ip' setting (RFC 8705 §2.1.2).
    /// </summary>
    public static SettingKey<string> TlsClientAuthSanIp =>
        new(OpenIdSettingNames.TlsClientAuthSanIp);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'tls_client_auth_san_email' setting (RFC 8705 §2.1.2).
    /// </summary>
    public static SettingKey<string> TlsClientAuthSanEmail =>
        new(OpenIdSettingNames.TlsClientAuthSanEmail);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'mtls_endpoint_alias_authority' setting (RFC 8705 §5).
    /// </summary>
    public static SettingKey<string> MtlsEndpointAliasAuthority =>
        new(OpenIdSettingNames.MtlsEndpointAliasAuthority);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'client_name' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<string> ClientName => new(OpenIdSettingNames.ClientName);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'client_uri' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<string> ClientUri => new(OpenIdSettingNames.ClientUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'logo_uri' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<string> LogoUri => new(OpenIdSettingNames.LogoUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'tos_uri' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<string> TosUri => new(OpenIdSettingNames.TosUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'policy_uri' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<string> PolicyUri => new(OpenIdSettingNames.PolicyUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'contacts' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<IReadOnlyCollection<string>> Contacts =>
        new(OpenIdSettingNames.Contacts);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'jwks' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<JsonElement> Jwks => new(OpenIdSettingNames.Jwks);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'software_id' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<string> SoftwareId => new(OpenIdSettingNames.SoftwareId);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the per-client 'software_version' setting (RFC 7591 §2).
    /// </summary>
    public static SettingKey<string> SoftwareVersion => new(OpenIdSettingNames.SoftwareVersion);
}
