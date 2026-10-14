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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> setting names.
/// </summary>
[PublicAPI]
public static class OpenIdSettingNames
{
    /// <summary>
    /// Contains the name of the <c>subject_claim_types</c> setting (the ordered list of claim types that identify the
    /// subject, tried in order; default <c>sub</c> then <c>nameidentifier</c>).
    /// </summary>
    public const string SubjectClaimTypes = "subject_claim_types";

    /// <summary>
    /// Contains the name of the <c>principal_issuer_claim</c> setting (the claim carrying the upstream issuer used to
    /// resolve an external connection identity).
    /// </summary>
    public const string PrincipalIssuerClaim = "principal_issuer_claim";

    /// <summary>
    /// Contains the name of the <c>federated_identity_join_claim</c> setting (the claim whose value deterministically
    /// links identities, such as an email address).
    /// </summary>
    public const string FederatedIdentityJoinClaim = "federated_identity_join_claim";

    /// <summary>
    /// Contains the name of the <c>federated_identity_verified_claim</c> setting (the claim indicating the join key is
    /// verified).
    /// </summary>
    public const string FederatedIdentityVerifiedClaim = "federated_identity_verified_claim";

    /// <summary>
    /// Contains the name of the <c>federated_identity_require_verified</c> setting (whether the join key must be
    /// verified before it is recorded and used to link identities).
    /// </summary>
    public const string FederatedIdentityRequireVerified = "federated_identity_require_verified";

    /// <summary>
    /// Contains the name of the <c>federated_identity_explicit_only</c> setting (whether linking is explicit-only and a
    /// new identity is never automatically attached to an existing principal).
    /// </summary>
    public const string FederatedIdentityExplicitOnly = "federated_identity_explicit_only";

    /// <summary>
    /// Contains the name of <c>access_token_encryption_alg_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionAlgValuesSupported =
        "access_token_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionEncValuesSupported =
        "access_token_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_encryption_required</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionRequired = "access_token_encryption_required";

    /// <summary>
    /// Contains the name of <c>access_token_encryption_zip_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenEncryptionZipValuesSupported =
        "access_token_encryption_zip_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_lifetime</c> setting.
    /// </summary>
    public const string AccessTokenLifetime = "access_token_lifetime";

    /// <summary>
    /// Contains the name of <c>access_token_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string AccessTokenSigningAlgValuesSupported =
        "access_token_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>access_token_type</c> setting.
    /// </summary>
    public const string AccessTokenType = "access_token_type";

    /// <summary>
    /// Contains the name of <c>acr_values_supported</c> setting.
    /// </summary>
    public const string AcrValuesSupported = "acr_values_supported";

    /// <summary>
    /// Contains the name of the <c>default_acr</c> setting: the baseline <c>acr</c> value the authorization server
    /// asserts for a single-factor authentication event when no stronger authentication context applies. Unset means
    /// no baseline (the <c>acr</c> claim is omitted unless a stronger context — such as multi-factor — is resolved).
    /// </summary>
    public const string DefaultAcr = "default_acr";

    /// <summary>
    /// Contains the name of the <c>always_include_auth_time</c> setting: whether the ID token always includes the
    /// <c>auth_time</c> claim when the end-user's authentication time is known, versus only when it is required (a
    /// <c>max_age</c> request or an essential <c>auth_time</c> claim request). Defaults to <see langword="true"/>.
    /// </summary>
    public const string AlwaysIncludeAuthTime = "always_include_auth_time";

    /// <summary>
    /// Contains the name of <c>allowed_identity_providers</c> setting.
    /// </summary>
    public const string AllowedIdentityProviders = "allowed_identity_providers";

    /// <summary>
    /// Contains the name of <c>allow_loopback_redirect</c> setting.
    /// </summary>
    public const string AllowLoopbackRedirect = "allow_loopback_redirect";

    /// <summary>
    /// Contains the name of <c>allow_unsafe_token_response</c> setting.
    /// </summary>
    public const string AllowUnsafeTokenResponse = "allow_unsafe_token_response";

    /// <summary>
    /// Contains the name of <c>authorization_authenticate_scheme</c> setting.
    /// </summary>
    public const string AuthorizationAuthenticateScheme = "authorization_authenticate_scheme";

    /// <summary>
    /// Contains the name of <c>authorization_challenge_scheme</c> setting.
    /// </summary>
    public const string AuthorizationChallengeScheme = "authorization_challenge_scheme";

    /// <summary>
    /// Contains the name of <c>authorization_code_lifetime</c> setting.
    /// </summary>
    public const string AuthorizationCodeLifetime = "authorization_code_lifetime";

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
    /// Contains the name of <c>claims_supported_is_strict</c> setting.
    /// </summary>
    public const string ClaimsSupportedIsStrict = "claims_supported_is_strict";

    /// <summary>
    /// Contains the name of <c>claim_types_supported</c> setting.
    /// </summary>
    public const string ClaimTypesSupported = "claim_types_supported";

    /// <summary>
    /// Contains the name of <c>clock_skew</c> setting.
    /// </summary>
    public const string ClockSkew = "clock_skew";

    /// <summary>
    /// Contains the name of <c>code_challenge_methods_supported</c> setting (the PKCE <c>code_challenge_method</c>
    /// transformations the server supports, RFC 7636 / RFC 8414).
    /// </summary>
    public const string CodeChallengeMethodsSupported = "code_challenge_methods_supported";

    /// <summary>
    /// Contains the name of <c>continue_authorization_lifetime</c> setting.
    /// </summary>
    public const string ContinueAuthorizationLifetime = "continue_authorization_lifetime";

    /// <summary>
    /// Contains the name of <c>device_code_lifetime</c> setting (the validity window of a pending device authorization
    /// request, RFC 8628).
    /// </summary>
    public const string DeviceCodeLifetime = "device_code_lifetime";

    /// <summary>
    /// Contains the name of <c>device_code_polling_interval</c> setting (the minimum interval the device must wait
    /// between polls of the token endpoint, RFC 8628).
    /// </summary>
    public const string DeviceCodePollingInterval = "device_code_polling_interval";

    /// <summary>
    /// Contains the name of <c>user_code_length</c> setting (the number of characters in a generated device
    /// <c>user_code</c>, RFC 8628).
    /// </summary>
    public const string UserCodeLength = "user_code_length";

    /// <summary>
    /// Contains the name of the per-client <c>backchannel_token_delivery_mode</c> setting (OpenID Connect CIBA): how
    /// the OP delivers the result — <c>poll</c>, <c>ping</c>, or <c>push</c>.
    /// </summary>
    public const string BackchannelTokenDeliveryMode = "backchannel_token_delivery_mode";

    /// <summary>
    /// Contains the name of the per-client <c>client_notification_endpoint</c> setting (OpenID Connect CIBA): the
    /// client endpoint the OP <c>POST</c>s to for <c>ping</c> and <c>push</c> delivery.
    /// </summary>
    public const string ClientNotificationEndpoint = "client_notification_endpoint";

    /// <summary>
    /// Contains the name of the server <c>backchannel_token_delivery_modes_supported</c> discovery setting (OpenID
    /// Connect CIBA).
    /// </summary>
    public const string BackchannelTokenDeliveryModesSupported =
        "backchannel_token_delivery_modes_supported";

    /// <summary>
    /// Contains the name of the server <c>backchannel_user_code_parameter_supported</c> discovery setting (OpenID
    /// Connect CIBA).
    /// </summary>
    public const string BackchannelUserCodeParameterSupported =
        "backchannel_user_code_parameter_supported";

    /// <summary>
    /// Contains the name of the server <c>backchannel_authentication_request_signing_alg_values_supported</c> discovery
    /// setting (OpenID Connect CIBA / FAPI-CIBA): the permitted <c>alg</c> values for a signed backchannel request object.
    /// </summary>
    public const string BackchannelAuthenticationRequestSigningAlgValuesSupported =
        "backchannel_authentication_request_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of the server <c>backchannel_authentication_request_encryption_alg_values_supported</c>
    /// discovery setting (OpenID Connect CIBA / FAPI-CIBA): the permitted key-management <c>alg</c> values for an
    /// encrypted backchannel request object.
    /// </summary>
    public const string BackchannelAuthenticationRequestEncryptionAlgValuesSupported =
        "backchannel_authentication_request_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of the server <c>backchannel_authentication_request_encryption_enc_values_supported</c>
    /// discovery setting (OpenID Connect CIBA / FAPI-CIBA): the permitted content-encryption <c>enc</c> values for an
    /// encrypted backchannel request object.
    /// </summary>
    public const string BackchannelAuthenticationRequestEncryptionEncValuesSupported =
        "backchannel_authentication_request_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of the server <c>backchannel_authentication_request_encryption_zip_values_supported</c>
    /// discovery setting (OpenID Connect CIBA): the permitted <c>zip</c> compression values for an encrypted
    /// backchannel request object.
    /// </summary>
    public const string BackchannelAuthenticationRequestEncryptionZipValuesSupported =
        "backchannel_authentication_request_encryption_zip_values_supported";

    /// <summary>
    /// Contains the name of <c>ciba_request_lifetime</c> setting (the validity window of a pending CIBA backchannel
    /// authentication request).
    /// </summary>
    public const string CibaRequestLifetime = "ciba_request_lifetime";

    /// <summary>
    /// Contains the name of <c>ciba_polling_interval</c> setting (the minimum interval the client must wait between
    /// polls of the token endpoint for a CIBA request).
    /// </summary>
    public const string CibaPollingInterval = "ciba_polling_interval";

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
    public const string IdTokenEncryptionAlgValuesSupported =
        "id_token_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>id_token_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string IdTokenEncryptionEncValuesSupported =
        "id_token_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>id_token_encryption_required</c> setting.
    /// </summary>
    public const string IdTokenEncryptionRequired = "id_token_encryption_required";

    /// <summary>
    /// Contains the name of <c>id_token_encryption_zip_values_supported</c> setting.
    /// </summary>
    public const string IdTokenEncryptionZipValuesSupported =
        "id_token_encryption_zip_values_supported";

    /// <summary>
    /// Contains the name of <c>id_token_lifetime</c> setting.
    /// </summary>
    public const string IdTokenLifetime = "id_token_lifetime";

    /// <summary>
    /// Contains the name of <c>id_token_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string IdTokenSigningAlgValuesSupported = "id_token_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>op_policy_uri</c> setting.
    /// </summary>
    public const string OpenIdProviderPolicyUri = "op_policy_uri";

    /// <summary>
    /// Contains the name of <c>op_tos_uri</c> setting.
    /// </summary>
    public const string OpenIdProviderTermsOfServiceUri = "op_tos_uri";

    /// <summary>
    /// Contains the name of <c>prompt_values_supported</c> setting.
    /// </summary>
    public const string PromptValuesSupported = "prompt_values_supported";

    /// <summary>
    /// Contains the name of <c>redirect_uris</c> setting.
    /// </summary>
    public const string RedirectUris = "redirect_uris";

    /// <summary>
    /// Contains the name of the per-client <c>post_logout_redirect_uris</c> setting (RP-Initiated Logout 1.0): the
    /// URIs the end-session endpoint is permitted to redirect back to after logout.
    /// </summary>
    public const string PostLogoutRedirectUris = "post_logout_redirect_uris";

    /// <summary>
    /// Contains the name of the per-client <c>backchannel_logout_uri</c> setting (Back-Channel Logout 1.0): the URI the
    /// OP sends the signed <c>logout_token</c> to when a session the client participates in is terminated.
    /// </summary>
    public const string BackChannelLogoutUri = "backchannel_logout_uri";

    /// <summary>
    /// Contains the name of the per-client <c>backchannel_logout_session_required</c> setting (Back-Channel Logout
    /// 1.0): whether the client requires a <c>sid</c> claim in the <c>logout_token</c> to identify the session.
    /// </summary>
    public const string BackChannelLogoutSessionRequired = "backchannel_logout_session_required";

    /// <summary>
    /// Contains the name of the server <c>backchannel_logout_supported</c> discovery setting (Back-Channel Logout 1.0).
    /// </summary>
    public const string BackChannelLogoutSupported = "backchannel_logout_supported";

    /// <summary>
    /// Contains the name of the server <c>backchannel_logout_session_supported</c> discovery setting (Back-Channel
    /// Logout 1.0): whether the OP includes a <c>sid</c> claim in <c>logout_token</c>s.
    /// </summary>
    public const string BackChannelLogoutSessionSupported = "backchannel_logout_session_supported";

    /// <summary>
    /// Contains the name of the per-client <c>frontchannel_logout_uri</c> setting (Front-Channel Logout 1.0): the URI
    /// the OP loads in a hidden iframe so the client can clear its own session in the user agent. Front-Channel Logout
    /// is available but disabled by default (it depends on third-party cookies the browser platform is removing); see
    /// <c>frontchannel_logout_supported</c>.
    /// </summary>
    public const string FrontChannelLogoutUri = "frontchannel_logout_uri";

    /// <summary>
    /// Contains the name of the per-client <c>frontchannel_logout_session_required</c> setting (Front-Channel Logout
    /// 1.0): whether the OP must append the <c>iss</c> and <c>sid</c> query parameters to the
    /// <c>frontchannel_logout_uri</c> so the client can identify the session being terminated.
    /// </summary>
    public const string FrontChannelLogoutSessionRequired = "frontchannel_logout_session_required";

    /// <summary>
    /// Contains the name of the server <c>frontchannel_logout_supported</c> discovery setting (Front-Channel Logout
    /// 1.0). Defaults to <see langword="false"/> — the mechanism is available but disabled by default because it
    /// depends on third-party cookies the browser platform is removing; prefer Back-Channel Logout.
    /// </summary>
    public const string FrontChannelLogoutSupported = "frontchannel_logout_supported";

    /// <summary>
    /// Contains the name of the server <c>frontchannel_logout_session_supported</c> discovery setting (Front-Channel
    /// Logout 1.0): whether the OP appends <c>iss</c>/<c>sid</c> to front-channel logout URIs. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    public const string FrontChannelLogoutSessionSupported =
        "frontchannel_logout_session_supported";

    /// <summary>
    /// Contains the name of the server <c>session_management_supported</c> setting (OpenID Connect Session Management
    /// 1.0): whether the OP advertises the <c>check_session_iframe</c>, emits the <c>session_state</c> authorization
    /// response parameter, and maintains the OP browser-state cookie. Defaults to <see langword="false"/> — the
    /// mechanism is available but disabled by default because it depends on third-party cookies the browser platform
    /// is removing.
    /// </summary>
    public const string SessionManagementSupported = "session_management_supported";

    /// <summary>
    /// Contains the name of <c>registration_endpoint_allow_anonymous</c> setting.
    /// </summary>
    public const string RegistrationEndpointAllowAnonymous =
        "registration_endpoint_allow_anonymous";

    /// <summary>
    /// Contains the name of <c>refresh_token_expiration_policy</c> setting.
    /// </summary>
    public const string RefreshTokenExpirationPolicy = "refresh_token_expiration_policy";

    /// <summary>
    /// Contains the name of <c>refresh_token_reuse_policy</c> setting.
    /// </summary>
    public const string RefreshTokenReusePolicy = "refresh_token_reuse_policy";

    /// <summary>
    /// Contains the name of <c>refresh_token_lifetime</c> setting.
    /// </summary>
    public const string RefreshTokenLifetime = "refresh_token_lifetime";

    /// <summary>
    /// Contains the name of <c>refresh_token_rotation_enabled</c> setting.
    /// </summary>
    public const string RefreshTokenRotationEnabled = "refresh_token_rotation_enabled";

    /// <summary>
    /// Contains the name of <c>request_object_encryption_alg_values_supported</c> setting.
    /// </summary>
    public const string RequestObjectEncryptionAlgValuesSupported =
        "request_object_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>request_object_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string RequestObjectEncryptionEncValuesSupported =
        "request_object_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>request_object_encryption_zip_values_supported</c> setting.
    /// </summary>
    public const string RequestObjectEncryptionZipValuesSupported =
        "request_object_encryption_zip_values_supported";

    /// <summary>
    /// Contains the name of <c>request_object_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string RequestObjectSigningAlgValuesSupported =
        "request_object_signing_alg_values_supported";

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
    public const string RequestUriRequireStrictContentType =
        "request_uri_require_strict_content_type";

    /// <summary>
    /// Contains the name of <c>request_uri_expected_content_type</c> setting.
    /// </summary>
    public const string RequestUriExpectedContentType = "request_uri_expected_content_type";

    /// <summary>
    /// Contains the name of <c>require_consent</c> setting.
    /// </summary>
    public const string RequireConsent = "require_consent";

    /// <summary>
    /// Contains the name of <c>require_pkce</c> setting.
    /// </summary>
    public const string RequireCodeChallenge = "require_pkce";

    /// <summary>
    /// Contains the name of <c>require_request_object</c> setting.
    /// </summary>
    public const string RequireRequestObject = "require_request_object";

    /// <summary>
    /// Contains the name of <c>require_request_uri_registration</c> setting.
    /// </summary>
    public const string RequireRequestUriRegistration = "require_request_uri_registration";

    /// <summary>
    /// Contains the name of <c>require_pushed_authorization_requests</c> setting (RFC 9126): whether the client must
    /// push its authorization request to the pushed authorization request endpoint before invoking the authorization
    /// endpoint.
    /// </summary>
    public const string RequirePushedAuthorizationRequests =
        "require_pushed_authorization_requests";

    /// <summary>
    /// Contains the name of <c>pushed_authorization_request_lifetime</c> setting (RFC 9126): the validity window of a
    /// pushed authorization request, after which its <c>request_uri</c> can no longer be used.
    /// </summary>
    public const string PushedAuthorizationRequestLifetime =
        "pushed_authorization_request_lifetime";

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
    /// Contains the name of <c>send_id_claims_in_access_token</c> setting.
    /// </summary>
    public const string SendIdClaimsInAccessToken = "send_id_claims_in_access_token";

    /// <summary>
    /// Contains the name of <c>send_profile_metadata_in_access_token</c> setting.
    /// </summary>
    public const string SendProfileMetadataInAccessToken = "send_profile_metadata_in_access_token";

    /// <summary>
    /// Contains the name of <c>send_profile_metadata_in_id_token</c> setting.
    /// </summary>
    public const string SendProfileMetadataInIdToken = "send_profile_metadata_in_id_token";

    /// <summary>
    /// Contains the name of <c>send_profile_metadata_in_user_info</c> setting.
    /// </summary>
    public const string SendProfileMetadataInUserInfo = "send_profile_metadata_in_user_info";

    /// <summary>
    /// Contains the name of <c>send_system_metadata_in_access_token</c> setting.
    /// </summary>
    public const string SendSystemMetadataInAccessToken = "send_system_metadata_in_access_token";

    /// <summary>
    /// Contains the name of <c>send_system_metadata_in_id_token</c> setting.
    /// </summary>
    public const string SendSystemMetadataInIdToken = "send_system_metadata_in_id_token";

    /// <summary>
    /// Contains the name of <c>send_system_metadata_in_user_info</c> setting.
    /// </summary>
    public const string SendSystemMetadataInUserInfo = "send_system_metadata_in_user_info";

    /// <summary>
    /// Contains the name of <c>service_documentation</c> setting.
    /// </summary>
    public const string ServiceDocumentation = "service_documentation";

    /// <summary>
    /// Contains the name of <c>pairwise_salt</c> setting.
    /// </summary>
    public const string PairwiseSalt = "pairwise_salt";

    /// <summary>
    /// Contains the name of <c>sector_identifier_uri</c> setting.
    /// </summary>
    public const string SectorIdentifierUri = "sector_identifier_uri";

    /// <summary>
    /// Contains the name of <c>subject_max_age</c> setting.
    /// </summary>
    public const string SubjectMaxAge = "subject_max_age";

    /// <summary>
    /// Contains the name of <c>subject_type</c> setting.
    /// </summary>
    public const string SubjectType = "subject_type";

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
    public const string TokenEndpointAuthSigningAlgValuesSupported =
        "token_endpoint_auth_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>ui_locales_supported</c> setting.
    /// </summary>
    public const string UiLocalesSupported = "ui_locales_supported";

    /// <summary>
    /// Contains the name of <c>userinfo_encryption_alg_values_supported</c> setting.
    /// </summary>
    public const string UserInfoEncryptionAlgValuesSupported =
        "userinfo_encryption_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>userinfo_encryption_enc_values_supported</c> setting.
    /// </summary>
    public const string UserInfoEncryptionEncValuesSupported =
        "userinfo_encryption_enc_values_supported";

    /// <summary>
    /// Contains the name of <c>userinfo_encryption_zip_values_supported</c> setting.
    /// </summary>
    public const string UserInfoEncryptionZipValuesSupported =
        "userinfo_encryption_zip_values_supported";

    /// <summary>
    /// Contains the name of <c>userinfo_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string UserInfoSigningAlgValuesSupported = "userinfo_signing_alg_values_supported";
}
