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
    /// Contains the name of the <c>authorization_response_iss_parameter_supported</c> setting (RFC 9207): whether the
    /// OP advertises that it returns the <c>iss</c> parameter in authorization responses.
    /// </summary>
    public const string AuthorizationResponseIssParameterSupported =
        "authorization_response_iss_parameter_supported";

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
    /// Contains the name of <c>client_assertion_max_lifetime</c> setting (the longest lifetime accepted for a
    /// <c>private_key_jwt</c> / <c>client_secret_jwt</c> client assertion, RFC 7523 §3).
    /// </summary>
    public const string ClientAssertionMaxLifetime = "client_assertion_max_lifetime";

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
    /// Contains the name of the per-client <c>id_token_encrypted_response_alg</c> setting (OpenID Connect Dynamic
    /// Client Registration 1.0 §2): the key-management algorithm the client registers for encrypting its ID tokens to
    /// its public key. When it is absent, ID tokens are not encrypted.
    /// </summary>
    public const string IdTokenEncryptedResponseAlg = "id_token_encrypted_response_alg";

    /// <summary>
    /// Contains the name of the per-client <c>id_token_encrypted_response_enc</c> setting (OpenID Connect Dynamic
    /// Client Registration 1.0 §2): the content-encryption algorithm of the client's encrypted ID tokens
    /// (<c>A128CBC-HS256</c> unless registered).
    /// </summary>
    public const string IdTokenEncryptedResponseEnc = "id_token_encrypted_response_enc";

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
    /// Contains the name of <c>id_token_lifetime</c> setting.
    /// </summary>
    public const string IdTokenLifetime = "id_token_lifetime";

    /// <summary>
    /// Contains the name of the per-client <c>id_token_signed_response_alg</c> setting (OpenID Connect Dynamic Client
    /// Registration 1.0 §2): the signature algorithm the client registers for its ID tokens (<c>RS256</c> unless
    /// registered).
    /// </summary>
    public const string IdTokenSignedResponseAlg = "id_token_signed_response_alg";

    /// <summary>
    /// Contains the name of <c>id_token_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string IdTokenSigningAlgValuesSupported = "id_token_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of the per-client <c>jwks_uri</c> setting: the URI of the client's JSON Web Key Set, resolved
    /// at runtime to obtain the client's public verification keys for <c>private_key_jwt</c> client authentication and
    /// signed request objects (JAR).
    /// </summary>
    public const string JwksUri = "jwks_uri";

    /// <summary>
    /// Contains the name of <c>jwks_uri_allowed_hosts</c> setting: an optional SSRF allow-list of hosts the client
    /// <c>jwks_uri</c> fetch is permitted to connect to.
    /// </summary>
    public const string JwksUriAllowedHosts = "jwks_uri_allowed_hosts";

    /// <summary>
    /// Contains the name of <c>jwks_uri_fetch_timeout</c> setting: the wall-clock cap on the client <c>jwks_uri</c>
    /// fetch.
    /// </summary>
    public const string JwksUriFetchTimeout = "jwks_uri_fetch_timeout";

    /// <summary>
    /// Contains the name of <c>jwks_uri_max_size_bytes</c> setting: the cap on the response body size accepted from the
    /// client <c>jwks_uri</c> fetch.
    /// </summary>
    public const string JwksUriMaxSizeBytes = "jwks_uri_max_size_bytes";

    /// <summary>
    /// Contains the name of <c>jwks_uri_cache_duration</c> setting: how long a successfully fetched client JSON Web Key
    /// Set is cached before it is re-fetched.
    /// </summary>
    public const string JwksUriCacheDuration = "jwks_uri_cache_duration";

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
    /// Contains the name of the tenant <c>session_absolute_lifetime</c> setting: the absolute lifetime applied to the
    /// server-side end-user session established at sign-in, after which the session expires regardless of activity. A
    /// value of <see cref="System.TimeSpan.Zero"/> disables the absolute lifetime (the session is bounded only by the
    /// idle timeout, if any). Defaults to <see cref="System.TimeSpan.Zero"/>. Not advertised in discovery.
    /// </summary>
    public const string SessionAbsoluteLifetime = "session_absolute_lifetime";

    /// <summary>
    /// Contains the name of the tenant <c>session_idle_timeout</c> setting: the sliding inactivity window after which
    /// an end-user session expires if it is not used. Each authorize request that resolves the session renews the
    /// window. A value of <see cref="System.TimeSpan.Zero"/> disables the idle timeout. Defaults to
    /// <see cref="System.TimeSpan.Zero"/>. Not advertised in discovery.
    /// </summary>
    public const string SessionIdleTimeout = "session_idle_timeout";

    /// <summary>
    /// Contains the name of the tenant <c>max_concurrent_sessions</c> setting: the maximum number of simultaneous
    /// sessions a single subject may hold. When a new sign-in would exceed the cap, the subject's oldest sessions are
    /// revoked so that at most this many remain active. A value of <c>0</c> disables the limit. Defaults to <c>0</c>.
    /// Not advertised in discovery.
    /// </summary>
    public const string MaxConcurrentSessions = "max_concurrent_sessions";

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
    /// Contains the name of <c>request_uri_allowed_hosts</c> setting: an optional SSRF allow-list of hosts the JAR
    /// <c>request_uri</c> fetch (RFC 9101) is permitted to connect to.
    /// </summary>
    public const string RequestUriAllowedHosts = "request_uri_allowed_hosts";

    /// <summary>
    /// Contains the name of <c>request_uri_fetch_timeout</c> setting: the wall-clock cap on the JAR <c>request_uri</c>
    /// fetch (RFC 9101).
    /// </summary>
    public const string RequestUriFetchTimeout = "request_uri_fetch_timeout";

    /// <summary>
    /// Contains the name of <c>request_uri_max_size_bytes</c> setting: the cap on the response body size accepted from
    /// the JAR <c>request_uri</c> fetch (RFC 9101).
    /// </summary>
    public const string RequestUriMaxSizeBytes = "request_uri_max_size_bytes";

    /// <summary>
    /// Contains the name of <c>require_consent</c> setting.
    /// </summary>
    public const string RequireConsent = "require_consent";

    /// <summary>
    /// Contains the name of <c>require_pkce</c> setting: whether every authorization request whose response type issues
    /// an authorization code must bind the code to a PKCE <c>code_challenge</c>.
    /// </summary>
    public const string RequireCodeChallenge = "require_pkce";

    /// <summary>
    /// Contains the name of <c>pkce_nonce_exemption</c> setting: an attestation that a client implements the OpenID
    /// Connect <c>nonce</c> mechanism correctly, so that where <c>require_pkce</c> is relaxed, its confidential OpenID
    /// Connect requests that carry a <c>nonce</c> may omit the PKCE <c>code_challenge</c>.
    /// </summary>
    public const string PkceNonceExemption = "pkce_nonce_exemption";

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
    /// Contains the name of <c>send_id_claims_in_access_token</c> setting.
    /// </summary>
    public const string SendIdClaimsInAccessToken = "send_id_claims_in_access_token";

    /// <summary>
    /// Contains the name of the per-client <c>token_exchange_allow_refresh_token</c> setting: whether this client may
    /// obtain a refresh token from an OAuth 2.0 Token Exchange
    /// (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see>) by requesting
    /// <c>requested_token_type=urn:ietf:params:oauth:token-type:refresh_token</c>. Defaults to <see langword="false"/>.
    /// </summary>
    public const string TokenExchangeAllowRefreshToken = "token_exchange_allow_refresh_token";

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
    /// Contains the name of <c>sector_identifier_uri_allowed_hosts</c> setting.
    /// </summary>
    public const string SectorIdentifierUriAllowedHosts = "sector_identifier_uri_allowed_hosts";

    /// <summary>
    /// Contains the name of <c>sector_identifier_uri_fetch_timeout</c> setting.
    /// </summary>
    public const string SectorIdentifierUriFetchTimeout = "sector_identifier_uri_fetch_timeout";

    /// <summary>
    /// Contains the name of <c>sector_identifier_uri_max_size_bytes</c> setting.
    /// </summary>
    public const string SectorIdentifierUriMaxSizeBytes = "sector_identifier_uri_max_size_bytes";

    /// <summary>
    /// Contains the name of the <c>security_profiles</c> setting: the named security baselines (for example
    /// <c>fapi2</c>) a server, tenant, or client selects to tighten the OP's effective settings and enforcement. The
    /// profiles accumulate down the <c>server → tenant → client</c> cascade: a scope can add a profile but never remove
    /// one that a scope above it selects, and every selected profile applies.
    /// </summary>
    public const string SecurityProfiles = "security_profiles";

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
    /// Contains the name of <c>token_endpoint_auth_methods_supported</c> setting: the client-authentication methods the
    /// server, tenant, or security profile permits. It is a ceiling over a client's registered
    /// <c>token_endpoint_auth_method</c>: a client whose registered method it excludes cannot authenticate.
    /// </summary>
    public const string TokenEndpointAuthMethodsSupported = "token_endpoint_auth_methods_supported";

    /// <summary>
    /// Contains the name of the <c>token_endpoint_auth_method</c> setting: the single client-authentication method a
    /// client is registered to use (RFC 7591 §2). It decides the client type — <c>none</c> registers a public client,
    /// any other method a confidential one — and it is the only method that authenticates the client at every endpoint
    /// that authenticates clients. A client that registers no method uses the RFC 7591 default,
    /// <c>client_secret_basic</c>, unless its server or tenant configures another.
    /// </summary>
    public const string TokenEndpointAuthMethod = "token_endpoint_auth_method";

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
    /// Contains the name of the per-client <c>userinfo_encrypted_response_alg</c> setting (OpenID Connect Dynamic
    /// Client Registration 1.0 §2): the key-management algorithm the client registers for encrypting its UserInfo
    /// responses to its public key. When it is absent, UserInfo responses are not encrypted.
    /// </summary>
    public const string UserInfoEncryptedResponseAlg = "userinfo_encrypted_response_alg";

    /// <summary>
    /// Contains the name of the per-client <c>userinfo_encrypted_response_enc</c> setting (OpenID Connect Dynamic
    /// Client Registration 1.0 §2): the content-encryption algorithm of the client's encrypted UserInfo responses
    /// (<c>A128CBC-HS256</c> unless registered).
    /// </summary>
    public const string UserInfoEncryptedResponseEnc = "userinfo_encrypted_response_enc";

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
    /// Contains the name of the per-client <c>userinfo_signed_response_alg</c> setting (OpenID Connect Dynamic Client
    /// Registration 1.0 §2): the signature algorithm the client registers for its UserInfo responses. When it is
    /// absent, UserInfo responses are not signed.
    /// </summary>
    public const string UserInfoSignedResponseAlg = "userinfo_signed_response_alg";

    /// <summary>
    /// Contains the name of <c>userinfo_signing_alg_values_supported</c> setting.
    /// </summary>
    public const string UserInfoSigningAlgValuesSupported = "userinfo_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>dpop_signing_alg_values_supported</c> setting (RFC 9449): the JWS signing algorithms
    /// the server accepts in a DPoP proof JWT. Advertised in discovery metadata and used to reject proofs signed with
    /// a non-permitted (or symmetric) algorithm.
    /// </summary>
    public const string DPoPSigningAlgValuesSupported = "dpop_signing_alg_values_supported";

    /// <summary>
    /// Contains the name of <c>dpop_proof_lifetime</c> setting (RFC 9449): the acceptable age window of a DPoP
    /// proof's <c>iat</c>, outside of which the proof is rejected as stale.
    /// </summary>
    public const string DPoPProofLifetime = "dpop_proof_lifetime";

    /// <summary>
    /// Contains the name of <c>dpop_nonce_required</c> setting (RFC 9449 §8): whether the server requires a DPoP
    /// proof to carry a server-provided <c>nonce</c>, challenging the client with <c>use_dpop_nonce</c> otherwise.
    /// </summary>
    public const string DPoPNonceRequired = "dpop_nonce_required";

    /// <summary>
    /// Contains the name of <c>require_dpop</c> setting: whether the client must present a valid DPoP proof at the
    /// token endpoint, with the issued access token bound to the proof key (RFC 9449).
    /// </summary>
    public const string RequireDPoP = "require_dpop";

    /// <summary>
    /// Contains the name of <c>tls_client_certificate_bound_access_tokens</c> setting (RFC 8705 §3.3): at server
    /// scope, whether the OP supports and advertises mutual-TLS client-certificate-bound access tokens; at client
    /// scope, whether the client's access tokens must be bound to its presented certificate.
    /// </summary>
    public const string TlsClientCertificateBoundAccessTokens =
        "tls_client_certificate_bound_access_tokens";

    /// <summary>
    /// Contains the name of the per-client <c>tls_client_auth_subject_dn</c> setting (RFC 8705 §2.1.2): the expected
    /// subject distinguished name (RFC 4514) of the client's PKI-issued certificate for <c>tls_client_auth</c>.
    /// </summary>
    public const string TlsClientAuthSubjectDn = "tls_client_auth_subject_dn";

    /// <summary>
    /// Contains the name of the per-client <c>tls_client_auth_san_dns</c> setting (RFC 8705 §2.1.2): the expected
    /// dNSName subject alternative name of the client's certificate for <c>tls_client_auth</c>.
    /// </summary>
    public const string TlsClientAuthSanDns = "tls_client_auth_san_dns";

    /// <summary>
    /// Contains the name of the per-client <c>tls_client_auth_san_uri</c> setting (RFC 8705 §2.1.2): the expected
    /// uniformResourceIdentifier subject alternative name of the client's certificate for <c>tls_client_auth</c>.
    /// </summary>
    public const string TlsClientAuthSanUri = "tls_client_auth_san_uri";

    /// <summary>
    /// Contains the name of the per-client <c>tls_client_auth_san_ip</c> setting (RFC 8705 §2.1.2): the expected
    /// iPAddress subject alternative name of the client's certificate for <c>tls_client_auth</c>.
    /// </summary>
    public const string TlsClientAuthSanIp = "tls_client_auth_san_ip";

    /// <summary>
    /// Contains the name of the per-client <c>tls_client_auth_san_email</c> setting (RFC 8705 §2.1.2): the expected
    /// rfc822Name subject alternative name of the client's certificate for <c>tls_client_auth</c>.
    /// </summary>
    public const string TlsClientAuthSanEmail = "tls_client_auth_san_email";

    /// <summary>
    /// Contains the name of <c>mtls_endpoint_alias_authority</c> setting (RFC 8705 §5): when set to a
    /// scheme-and-authority (for example <c>https://mtls.op.example</c>), the discovery document advertises an
    /// <c>mtls_endpoint_aliases</c> object whose endpoint URLs are rewritten to that mutual-TLS authority.
    /// </summary>
    public const string MtlsEndpointAliasAuthority = "mtls_endpoint_alias_authority";

    /// <summary>
    /// Contains the name of the per-client <c>client_name</c> setting (RFC 7591 §2): the human-readable name of the
    /// client presented to the end-user, for example on the consent page.
    /// </summary>
    public const string ClientName = "client_name";

    /// <summary>
    /// Contains the name of the per-client <c>client_uri</c> setting (RFC 7591 §2): the URL of the client's home page.
    /// </summary>
    public const string ClientUri = "client_uri";

    /// <summary>
    /// Contains the name of the per-client <c>logo_uri</c> setting (RFC 7591 §2): the URL of the client's logo.
    /// </summary>
    public const string LogoUri = "logo_uri";

    /// <summary>
    /// Contains the name of the per-client <c>tos_uri</c> setting (RFC 7591 §2): the URL of the client's terms of
    /// service.
    /// </summary>
    public const string TosUri = "tos_uri";

    /// <summary>
    /// Contains the name of the per-client <c>policy_uri</c> setting (RFC 7591 §2): the URL of the client's privacy
    /// policy.
    /// </summary>
    public const string PolicyUri = "policy_uri";

    /// <summary>
    /// Contains the name of the per-client <c>contacts</c> setting (RFC 7591 §2): the email addresses of the people
    /// responsible for the client.
    /// </summary>
    public const string Contacts = "contacts";

    /// <summary>
    /// Contains the name of the per-client <c>jwks</c> setting (RFC 7591 §2): the JSON Web Key Set document the client
    /// registered by value, kept as registered so a registration read returns it. Its keys are imported as the
    /// client's secrets when the client registers.
    /// </summary>
    public const string Jwks = "jwks";

    /// <summary>
    /// Contains the name of the per-client <c>software_id</c> setting (RFC 7591 §2): the identifier of the client
    /// software, stable across its instances and versions.
    /// </summary>
    public const string SoftwareId = "software_id";

    /// <summary>
    /// Contains the name of the per-client <c>software_version</c> setting (RFC 7591 §2): the version of the client
    /// software identified by <c>software_id</c>.
    /// </summary>
    public const string SoftwareVersion = "software_version";
}
