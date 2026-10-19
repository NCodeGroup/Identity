#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Settings;
using static NCode.Identity.Settings.SettingMerge;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="SettingDescriptor"/> instances supported by this library.
/// </summary>
/// <remarks>
/// Every descriptor declares its domain through <see cref="SettingDescriptor{TValue}.Rule"/>. A rule whose domain is
/// defined by registered strategies consults them: an algorithm setting accepts the registered algorithms of its kind,
/// and <c>security_profiles</c> accepts the names of the registered <see cref="ISecurityProfile"/> implementations.
/// </remarks>
// See ADR-0140.
internal class DefaultSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IEnumerable<ISecurityProfile> securityProfiles
) : ICollectionDataSource<SettingDescriptor>
{
    private const bool IsStdDiscoverable = true;
    private const bool IsNonStdDiscoverable = false;

    // RFC 8628 §6.1: eight characters from the 20-character default alphabet give about 34.5 bits of entropy, the
    // floor that keeps a user_code impractical to guess under rate limiting; the ceiling keeps it typeable.
    private const int MinUserCodeLength = 8;
    private const int MaxUserCodeLength = 32;

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ImmutableArray<string> SecurityProfileNames { get; } =
    [.. securityProfiles.Select(profile => profile.ProfileName).Order(StringComparer.Ordinal)];

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // subject_claim_types: ordered claim types that identify the end-user subject (default sub, then nameidentifier). (ADR-0053)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SubjectClaimTypes,
                Rule = OpenIdSettingRules.ClaimTypes(),
                Default = OpenIdConstants.DefaultSubjectClaimTypes,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // principal_issuer_claim: claim identifying the upstream issuer of a federated principal (default "iss"). (ADR-0035)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PrincipalIssuerClaim,
                Rule = OpenIdSettingRules.Token(),
                Default = "iss",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_join_claim: claim used to auto-link a federated identity to an existing principal (default "email"). (ADR-0035)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FederatedIdentityJoinClaim,
                Rule = OpenIdSettingRules.Token(),
                Default = "email",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_verified_claim: boolean claim proving the join claim is verified (default "email_verified"). (ADR-0035)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FederatedIdentityVerifiedClaim,
                Rule = OpenIdSettingRules.Token(),
                Default = "email_verified",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_require_verified: require the join claim be verified before auto-linking (default true). (ADR-0035)
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FederatedIdentityRequireVerified,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // federated_identity_explicit_only: only link federated identities that were explicitly provisioned (default false). (ADR-0035)
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FederatedIdentityExplicitOnly,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // access_token_encryption_alg_values_supported: permitted "alg" values for access-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported,
                Rule = KeyManagementAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_enc_values_supported: permitted "enc" values for access-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_required: whether issued access tokens must be encrypted (default false).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // access_token_encryption_zip_values_supported: permitted "zip" compression values for access-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionZipValuesSupported,
                Rule = CompressionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_lifetime: validity window for issued access tokens (default 5 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AccessTokenLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // access_token_signing_alg_values_supported: permitted "alg" values for access-token signing.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenSigningAlgValuesSupported,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_type: 'typ' JOSE header media type for issued JWT access tokens (RFC 9068 §2.1 mandates
            // 'at+jwt', which enables the explicit-typing / token-substitution defense of RFC 8725 §3.11).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AccessTokenType,
                Rule = OpenIdSettingRules.AccessTokenType(),
                Default = JoseTokenTypes.AccessTokenJwt,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // acr_values_supported: Authentication Context Class Reference values the server honors.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AcrValuesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // default_acr: the baseline acr value asserted for a single-factor authentication event (OP behavior, not
            // advertised). Replace: a child's value overrides the parent's.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.DefaultAcr,
                Rule = OpenIdSettingRules.Token(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // always_include_auth_time: whether the ID token always includes auth_time when known, versus only when
            // required (max_age or an essential auth_time claim request). Default true (OP behavior, not advertised).
            // Ceiling (And): once a parent turns it off, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AlwaysIncludeAuthTime,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allow_loopback_redirect: permit loopback IP redirect URIs for native clients (default true).
            // Ceiling (And): once a parent forbids it, a child cannot re-allow it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowLoopbackRedirect,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allowed_identity_providers: identity providers a client may use; unset permits any.
            // Ceiling (Intersect): a child narrows the parent's set; a set (or narrowed-to-empty) list denies all others (fail-closed).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AllowedIdentityProviders,
                Rule = SettingRules.Each(SettingRules.NonEmpty()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // authorization_authenticate_scheme: ASP.NET Core auth scheme used to authenticate the end-user (default application cookie).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationAuthenticateScheme,
                Rule = SettingRules.NonEmpty(),

                // we are compatible with Microsoft.AspNetCore.Identity
                // do not use the "Identity.External" scheme as it is only for external identity providers
                Default = IdentityConstants.ApplicationScheme,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // authorization_challenge_scheme: ASP.NET Core auth scheme used to challenge the end-user to log in (default application cookie).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationChallengeScheme,
                Rule = SettingRules.NonEmpty(),

                // we are compatible with Microsoft.AspNetCore.Identity
                // do not use the "Identity.External" scheme as it is only for external identity providers
                Default = IdentityConstants.ApplicationScheme,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // authorization_code_lifetime: validity window for issued authorization codes (default 5 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AuthorizationCodeLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // authorization_response_iss_parameter_supported: advertises that the OP returns the iss parameter in
            // authorization responses (RFC 9207). The behavior is always emitted; this flag advertises it. Default true.
            // Floor (Or): once a parent advertises it, a child cannot un-advertise it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AuthorizationResponseIssParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // claims_locales_supported: BCP47 locales for which the server can return localized claim values.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsLocalesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // claims_parameter_supported: whether the OIDC "claims" request parameter is honored (default true; ADR-0039).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // claims_supported: claim names the OP may return (advertised in discovery; default supplied by the baseline provider).
            // Override (Replace). Enforced as an allow-list only when claims_supported_is_strict is true.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // claims_supported_is_strict: when true, emitted claims are filtered to claims_supported (default false).
            // Floor (Or): once a parent enables strict filtering, a child cannot disable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsSupportedIsStrict,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // claim_types_supported: OIDC claim types the server can emit (default "normal"; aggregated/distributed are not implemented).
            // Ceiling (Intersect): advisory discovery only (no runtime consumer); reflects a fixed capability.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimTypesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.ClaimType()),
                Default = [OpenIdConstants.ClaimTypes.Normal],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // client_assertion_max_lifetime: longest lifetime accepted for a private_key_jwt / client_secret_jwt
            // assertion, from its earliest 'iat'/'nbf' to its 'exp' (RFC 7523 §3; default 5 minutes, ADR-0124).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ClientAssertionMaxLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // clock_skew: allowed leeway when validating token time claims (default 5 minutes).
            // Ceiling (Min): a child may only reduce the tolerance; raise the server value to allow more.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ClockSkew,
                Rule = OpenIdSettingRules.NonNegativeDuration(),
                Default = TimeSpan.FromMinutes(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // continue_authorization_lifetime: validity window for a paused ("continue") authorization flow (default 15 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ContinueAuthorizationLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(15),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // device_code_lifetime: validity window for a pending device authorization request (default 10 minutes; RFC 8628).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.DeviceCodeLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(10),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // device_code_polling_interval: minimum interval the device must wait between token-endpoint polls (default 5 seconds; RFC 8628).
            // Floor (Max): a child may only lengthen it (poll more slowly); it cannot poll faster than a parent allows.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.DeviceCodePollingInterval,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromSeconds(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Max,
            };

            // user_code_length: number of characters in a generated device user_code (default 8; RFC 8628).
            // Floor (Max): a child may only lengthen it (stronger); it cannot shorten below a parent's value.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.UserCodeLength,
                Rule = SettingRules.Between(MinUserCodeLength, MaxUserCodeLength),
                Default = 8,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Max,
            };

            // backchannel_token_delivery_mode: per-client CIBA delivery mode (poll/ping/push; default poll).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.BackchannelTokenDeliveryMode,
                Rule = OpenIdSettingRules.BackchannelTokenDeliveryMode(),
                Default = OpenIdConstants.BackchannelTokenDeliveryModes.Poll,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // client_notification_endpoint: per-client CIBA endpoint the OP POSTs ping/push notifications to. CIBA
            // Core §4 requires an https URL; empty (the default) means the client registered none, which a ping or
            // push client is rejected for. Override (Replace): each client specifies its own endpoint.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ClientNotificationEndpoint,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.HttpsUri()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_token_delivery_modes_supported: CIBA delivery modes the server supports.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.BackchannelTokenDeliveryModesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.BackchannelTokenDeliveryMode()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // backchannel_user_code_parameter_supported: whether the server supports the CIBA user_code parameter (default false).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackchannelUserCodeParameterSupported,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // backchannel_authentication_request_signing_alg_values_supported: permitted "alg" values for a signed CIBA request object.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.BackchannelAuthenticationRequestSigningAlgValuesSupported,
                Rule = SignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // backchannel_authentication_request_encryption_alg_values_supported: permitted key-management "alg" values for an encrypted CIBA request object.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            // Non-discoverable: CIBA Core defines no encryption metadata for the backchannel request (only the signing
            // alg variant), so advertising it is a non-standard parameter — the capability is still honored un-advertised.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name =
                    OpenIdSettingNames.BackchannelAuthenticationRequestEncryptionAlgValuesSupported,
                Rule = KeyManagementAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // backchannel_authentication_request_encryption_enc_values_supported: permitted content-encryption "enc" values for an encrypted CIBA request object.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            // Non-discoverable: see the alg variant above — no standard CIBA discovery parameter exists for this.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name =
                    OpenIdSettingNames.BackchannelAuthenticationRequestEncryptionEncValuesSupported,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // backchannel_authentication_request_encryption_zip_values_supported: permitted "zip" compression values for an encrypted CIBA request object.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name =
                    OpenIdSettingNames.BackchannelAuthenticationRequestEncryptionZipValuesSupported,
                Rule = CompressionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // ciba_request_lifetime: validity window for a pending CIBA request (default 10 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.CibaRequestLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(10),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // ciba_polling_interval: minimum interval the client must wait between token-endpoint polls (default 5 seconds).
            // Floor (Max): a child may only lengthen it (poll more slowly); it cannot poll faster than a parent allows.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.CibaPollingInterval,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromSeconds(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Max,
            };

            // display_values_supported: OIDC "display" parameter values the server supports.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.DisplayValuesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.DisplayValue()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // grant_types_supported: OAuth grant types the server permits (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; capability is enforced by grant handlers (ADR-0010/0026).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.GrantTypesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.GrantType()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_alg_values_supported: permitted "alg" values for id-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported,
                Rule = KeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_enc_values_supported: permitted "enc" values for id-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionEncValuesSupported,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_required: whether issued id tokens must be encrypted (default false).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // id_token_encryption_zip_values_supported: permitted "zip" compression values for id-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionZipValuesSupported,
                Rule = CompressionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_lifetime: validity window for issued id tokens (default 5 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.IdTokenLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // id_token_signing_alg_values_supported: permitted "alg" values for id-token signing.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenSigningAlgValuesSupported,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // op_policy_uri: URL of the OP's data-usage policy document.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderPolicyUri,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // op_tos_uri: URL of the OP's terms-of-service document.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderTermsOfServiceUri,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // prompt_values_supported: OIDC "prompt" values the server supports (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.PromptValuesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.PromptValue()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // redirect_uris: the registered redirect URIs for a client.
            // Override (Replace): each scope specifies its own set (per-client registration, not a cascade ceiling).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RedirectUris,
                Rule = SettingRules.Each(OpenIdSettingRules.RedirectUri()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // post_logout_redirect_uris: the registered post-logout redirect URIs for a client (RP-Initiated Logout).
            // Override (Replace): each client specifies its own set (per-client registration, not a cascade ceiling).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.PostLogoutRedirectUris,
                Rule = SettingRules.Each(OpenIdSettingRules.RedirectUri()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_uri: the per-client URI the OP POSTs the logout_token to (Back-Channel Logout).
            // Override (Replace): each client specifies its own endpoint.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.BackChannelLogoutUri,
                Rule = OpenIdSettingRules.LogoutUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_session_required: whether the client requires a sid in the logout_token.
            // Override (Replace): each client specifies its own requirement.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackChannelLogoutSessionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_supported: server advertises Back-Channel Logout support (default true).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackChannelLogoutSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_session_supported: server advertises that logout_tokens include a sid (default true).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackChannelLogoutSessionSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_uri: the per-client URI the OP loads in a hidden iframe (Front-Channel Logout).
            // Override (Replace): each client specifies its own endpoint.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutUri,
                Rule = OpenIdSettingRules.LogoutUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_session_required: whether the OP appends iss/sid to the client's frontchannel_logout_uri.
            // Override (Replace): each client specifies its own requirement.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutSessionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_supported: server advertises Front-Channel Logout support (default false — the
            // mechanism is available but disabled by default because it depends on third-party cookies). (ADR-0088)
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutSupported,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_session_supported: server advertises that it appends iss/sid to front-channel
            // logout URIs (default false).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutSessionSupported,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // session_management_supported: gates the check_session_iframe advertisement, the session_state
            // authorization response parameter, and the OP browser-state cookie. Non-discoverable (the standard
            // discovery signal is the check_session_iframe URL). Default false — available but disabled by default
            // because it depends on third-party cookies the browser platform is removing. (ADR-0088)
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SessionManagementSupported,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // session_absolute_lifetime: absolute lifetime of the server-side end-user session, after which it expires
            // regardless of activity. TimeSpan.Zero disables it (bounded only by the idle timeout, if any). Default
            // Zero. Not advertised (leaking session policy aids attackers). Override (Replace): a tenant fully replaces
            // the value. (F25)
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SessionAbsoluteLifetime,
                Rule = OpenIdSettingRules.NonNegativeDuration(),
                Default = TimeSpan.Zero,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // session_idle_timeout: sliding inactivity window after which the end-user session expires if unused; each
            // authorize request that resolves the session renews it. TimeSpan.Zero disables it. Default Zero. Not
            // advertised. Override (Replace): a tenant fully replaces the value. (F25)
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SessionIdleTimeout,
                Rule = OpenIdSettingRules.NonNegativeDuration(),
                Default = TimeSpan.Zero,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // max_concurrent_sessions: cap on the number of simultaneous sessions a subject may hold; the oldest beyond
            // the cap are revoked at sign-in. 0 disables the limit. Default 0. Not advertised. Override (Replace): a
            // tenant fully replaces the value. (F25)
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.MaxConcurrentSessions,
                Rule = SettingRules.AtLeast(0),
                Default = 0,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // registration_endpoint_allow_anonymous: whether the dynamic client registration endpoint accepts
            // anonymous (no initial access token) requests (default false). (ADR-0063)
            // Floor (And): once a parent disallows anonymous registration, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RegistrationEndpointAllowAnonymous,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // refresh_token_expiration_policy: how a refresh token's expiration is computed — absolute or sliding (default
            // absolute). Either way the token expires; a never-expiring refresh token is not offered (RFC 9700 §4.14.2).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RefreshTokenExpirationPolicy,
                Rule = OpenIdSettingRules.RefreshTokenExpirationPolicy(),
                Default = OpenIdConstants.RefreshTokenExpirationPolicy.Absolute,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_reuse_policy: how the server responds when a revoked refresh token is replayed —
            // none (reject + audit only) or revoke_all (revoke the whole rotation family), per RFC 9700. Secure by
            // default: revoke_all.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RefreshTokenReusePolicy,
                Rule = OpenIdSettingRules.RefreshTokenReusePolicy(),
                Default = OpenIdConstants.RefreshTokenReusePolicy.RevokeAll,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_lifetime: validity window for issued refresh tokens (default 30 days).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RefreshTokenLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromDays(30.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // refresh_token_rotation_enabled: issue a new refresh token on each use and revoke the prior one (default false).
            // Floor (Or): once a parent enables rotation, a child cannot disable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RefreshTokenRotationEnabled,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // request_object_encryption_alg_values_supported: permitted "alg" values for encrypting request objects.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported,
                Rule = KeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_encryption_enc_values_supported: permitted "enc" values for encrypting request objects.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_encryption_zip_values_supported: permitted "zip" compression values for encrypting request objects.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionZipValuesSupported,
                Rule = CompressionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_signing_alg_values_supported: permitted "alg" values for signing request objects.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,
                Rule = SignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_expected_audience: expected "aud" of a client's request object; empty disables audience validation (default empty).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestObjectExpectedAudience,
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // request_parameter_supported: whether a passed-by-value "request" object parameter is accepted (default true).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // request_uri_parameter_supported: whether a "request_uri" parameter is accepted (default true).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // request_uri_require_strict_content_type: require the request_uri response to use the expected content type (default false).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriRequireStrictContentType,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // request_uri_expected_content_type: expected content type when fetching a request_uri (default application/oauth-authz-req+jwt).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestUriExpectedContentType,
                Rule = OpenIdSettingRules.MediaType(),
                Default = "application/oauth-authz-req+jwt",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // request_uri_allowed_hosts: SSRF allow-list for the JAR request_uri fetch (RFC 9101). When set, the
            // request_uri host must appear in this list; unset = no host allow-list, though the always-on private/
            // loopback/link-local address block in DefaultOutboundHttpForgeryGuard still applies. Not advertised.
            // Ceiling (Intersect): a child narrows the parent's set.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestUriAllowedHosts,
                Rule = SettingRules.Each(OpenIdSettingRules.HostName()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_uri_fetch_timeout: wall-clock cap on the JAR request_uri fetch (RFC 9101). Not advertised.
            // Ceiling (Min): a child may only shorten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RequestUriFetchTimeout,
                Rule = OpenIdSettingRules.Timeout(),
                Default = TimeSpan.FromSeconds(10),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // request_uri_max_size_bytes: cap on the response body size accepted from the JAR request_uri fetch
            // (RFC 9101), defending against an oversized-response resource-exhaustion vector. Not advertised.
            // Ceiling (Min): a child may only shrink it.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.RequestUriMaxSizeBytes,
                Rule = SettingRules.AtLeast(1),
                Default = 100 * 1024,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // require_consent: whether the authorization endpoint must obtain (and persist) end-user consent for the
            // requested scopes before issuing a grant, even when a valid session exists (default false; prompt=consent
            // forces it regardless). Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireConsent,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // require_pkce: whether every authorization request whose response type issues an authorization code must
            // bind the code to a PKCE code_challenge (default true; RFC 9700 §2.1.1, OAuth 2.1 §4.1.1). Only the
            // server's configuration can relax it; where it is relaxed, a confidential client attested by
            // pkce_nonce_exemption may use the OpenID Connect nonce instead. A public client always uses PKCE
            // (ADR-0144).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireCodeChallenge,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // pkce_nonce_exemption: an attestation that the client implements the OpenID Connect nonce mechanism
            // correctly — the "reasonable assurance" of OAuth 2.1 §7.5.1.1 — so where require_pkce is relaxed, its
            // confidential OpenID Connect requests that carry a nonce may omit the PKCE code_challenge (default false).
            // It is an administrator's statement about a known client: dynamic client registration never sets it, and
            // it has no effect while require_pkce is in force (ADR-0144).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.PkceNonceExemption,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // require_request_object: whether an authorization request must be passed as a signed request object
            // (via the request or request_uri parameter) rather than plain query parameters (default false).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireRequestObject,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // require_request_uri_registration: whether request_uri values must be pre-registered by the client.
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireRequestUriRegistration,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // require_pushed_authorization_requests: whether the client must push its authorization request to the PAR
            // endpoint before invoking the authorization endpoint (default false; RFC 9126 §5).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequirePushedAuthorizationRequests,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // pushed_authorization_request_lifetime: validity window of a pushed authorization request (default 90
            // seconds; RFC 9126 §2.2). Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.PushedAuthorizationRequestLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromSeconds(90),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // dpop_signing_alg_values_supported: JWS algorithms accepted in a DPoP proof JWT (RFC 9449). The baseline
            // is the server's asymmetric signing set (supplied by the algorithm pipeline); symmetric algorithms are
            // never acceptable for a DPoP proof. Ceiling (Intersect): a child narrows the parent's set.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.DPoPSigningAlgValuesSupported,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // dpop_proof_lifetime: acceptable age window of a DPoP proof's iat (RFC 9449 §4.3), default 60 seconds.
            // Ceiling (Min): a child may only tighten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.DPoPProofLifetime,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromSeconds(60),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // dpop_nonce_required: whether the server demands a server-provided nonce in a DPoP proof (RFC 9449 §8),
            // default false. Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.DPoPNonceRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // require_dpop: whether the client must present a valid DPoP proof at the token endpoint (RFC 9449),
            // default false. Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireDPoP,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // tls_client_certificate_bound_access_tokens: at server scope, whether the OP supports/advertises
            // mutual-TLS client-certificate-bound access tokens; at client scope, whether the client's access tokens
            // must be bound to its certificate (RFC 8705 §3.3). Secure-by-default false (only advertise once the
            // deployment has wired certificate forwarding). Floor (Or).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.TlsClientCertificateBoundAccessTokens,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // mtls_endpoint_alias_authority: scheme-and-authority that the discovery handler uses to synthesize the
            // mtls_endpoint_aliases object (RFC 8705 §5). Not itself advertised (the handler emits the derived object);
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.MtlsEndpointAliasAuthority,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.HttpsOrigin()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // tls_client_auth_subject_dn / _san_dns / _san_uri / _san_ip / _san_email: the expected certificate identity
            // a PKI-validated tls_client_auth client must present (RFC 8705 §2.1.2); exactly one is configured per
            // client. Not advertised. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSubjectDn,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.DistinguishedName()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanDns,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.DnsName()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanUri,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.AbsoluteUri()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanIp,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.IpAddress()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanEmail,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.EmailAddress()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // response_modes_supported: OAuth response modes the server supports (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseModesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.ResponseMode()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // response_types_supported: the whole response_type values the server accepts, in canonical form (default
            // supplied by the baseline provider). Ceiling (Intersect): a child narrows the parent's set; discovery
            // advertises the values as they are, and the authorization endpoint accepts exactly them (ADR-0141).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseTypesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.ResponseType()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // send_id_claims_in_access_token: also place id-token claims into the access token (default false).
            // Override (Replace): behavioral toggle; a child fully replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendIdClaimsInAccessToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // token_exchange_allow_refresh_token: whether this client may obtain a refresh token from an OAuth 2.0
            // Token Exchange (RFC 8693) by requesting requested_token_type=...:refresh_token (default false — issuing a
            // long-lived refresh token from an exchange is opt-in per client). Override (Replace): a behavioral toggle;
            // a child fully replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.TokenExchangeAllowRefreshToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // send_profile_metadata_in_access_token: project the account's owner-updatable profile metadata bag into
            // the access token as a single JSON object claim (default false). Override (Replace): a child fully
            // replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendProfileMetadataInAccessToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // send_profile_metadata_in_id_token: project the account's owner-updatable profile metadata bag into the id
            // token as a single JSON object claim (default false). Override (Replace): a child fully replaces the
            // parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendProfileMetadataInIdToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // send_profile_metadata_in_user_info: project the account's owner-updatable profile metadata bag into the
            // UserInfo response as a single JSON object claim (default false). Override (Replace): a child fully
            // replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendProfileMetadataInUserInfo,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // send_system_metadata_in_access_token: project the account's server-controlled system metadata bag into
            // the access token as a single JSON object claim (default false). This is the authorization-appropriate
            // destination for system metadata. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendSystemMetadataInAccessToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // send_system_metadata_in_id_token: project the account's server-controlled system metadata bag into the id
            // token as a single JSON object claim (default false). Override (Replace): a child fully replaces the
            // parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendSystemMetadataInIdToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // send_system_metadata_in_user_info: project the account's server-controlled system metadata bag into the
            // UserInfo response as a single JSON object claim (default false). Override (Replace): a child fully
            // replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendSystemMetadataInUserInfo,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // service_documentation: URL of human-readable developer documentation for the server.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ServiceDocumentation,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // subject_max_age: maximum age of the end-user authentication a client will accept. Has no default.
            // Ceiling (Min): a child may only shorten it; a shorter value demands fresher authentication.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SubjectMaxAge,
                Rule = OpenIdSettingRules.NonNegativeDuration(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // subject_type: the subject identifier type a client uses — public or pairwise.
            // Default (public): a client with no explicit value sees the stable public subject.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SubjectType,
                Rule = OpenIdSettingRules.SubjectType(),
                Default = OpenIdConstants.SubjectTypes.Public,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // sector_identifier_uri: groups clients into a single pairwise sector (OpenID Connect Core 1.0 §8.1); the
            // host of this URI is the pairwise sector identifier. When unset the sector falls back to the common host
            // of the client's redirect_uris (or the client_id when that is ambiguous). Not advertised in discovery.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SectorIdentifierUri,
                Rule = OpenIdSettingRules.HttpsUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // jwks_uri: the per-client URI of the client's JSON Web Key Set, resolved at runtime to obtain the client's
            // public verification keys for private_key_jwt client authentication and signed request objects (JAR). Not
            // advertised in discovery (it is a per-client value). Override (Replace): a child fully replaces the parent.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.JwksUri,
                Rule = OpenIdSettingRules.HttpsUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // RFC 7591 §2 client metadata: client_name, client_uri, logo_uri, tos_uri, policy_uri, contacts, jwks,
            // software_id, software_version. Registered so a registration (or the management API) stores them as typed
            // settings rather than untyped extras; client_name is shown on the consent page, and jwks keeps the key set
            // the client registered by value (its keys are imported as client secrets). No defaults: absent means the
            // client did not provide it. Not advertised. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ClientName,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ClientUri,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.LogoUri,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TosUri,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PolicyUri,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.Contacts,
                Rule = SettingRules.Each(SettingRules.NonEmpty()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<JsonElement>
            {
                Name = OpenIdSettingNames.Jwks,
                Rule = OpenIdSettingRules.JwkSet(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SoftwareId,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SoftwareVersion,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // jwks_uri_allowed_hosts: SSRF allow-list for the runtime client jwks_uri fetch. When set, the jwks_uri
            // host must appear in this list; unset = no host allow-list, though the always-on private/loopback/
            // link-local address block still applies. Not advertised. Ceiling (Intersect): a child narrows the parent.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.JwksUriAllowedHosts,
                Rule = SettingRules.Each(OpenIdSettingRules.HostName()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // jwks_uri_fetch_timeout: wall-clock cap on the runtime client jwks_uri fetch. Not advertised.
            // Ceiling (Min): a child may only shorten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.JwksUriFetchTimeout,
                Rule = OpenIdSettingRules.Timeout(),
                Default = TimeSpan.FromSeconds(10),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // jwks_uri_max_size_bytes: cap on the response body size accepted from the runtime client jwks_uri fetch,
            // defending against an oversized-response resource-exhaustion vector. Not advertised. Ceiling (Min): a
            // child may only shrink it.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.JwksUriMaxSizeBytes,
                Rule = SettingRules.AtLeast(1),
                Default = 100 * 1024,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // jwks_uri_cache_duration: how long a successfully fetched client JWKS is cached before re-fetch, bounding
            // both key-rotation propagation delay and fetch load. Not advertised. Ceiling (Min): a child may only
            // shorten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.JwksUriCacheDuration,
                Rule = OpenIdSettingRules.NonNegativeDuration(),
                Default = TimeSpan.FromMinutes(15),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // sector_identifier_uri_allowed_hosts: SSRF allow-list for the registration-time sector_identifier_uri
            // fetch (OpenID Connect Core 1.0 §8.1). When set, the sector_identifier_uri host must appear in this list;
            // unset = no host allow-list (ADR-0010), though the always-on private/loopback/link-local address block
            // still applies. Not advertised. Ceiling (Intersect): a child narrows the parent's set.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SectorIdentifierUriAllowedHosts,
                Rule = SettingRules.Each(OpenIdSettingRules.HostName()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // sector_identifier_uri_fetch_timeout: wall-clock cap on the registration-time sector_identifier_uri fetch.
            // Not advertised. Ceiling (Min): a child may only shorten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SectorIdentifierUriFetchTimeout,
                Rule = OpenIdSettingRules.Timeout(),
                Default = TimeSpan.FromSeconds(10),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // sector_identifier_uri_max_size_bytes: cap on the response body size accepted from the registration-time
            // sector_identifier_uri fetch, defending against an oversized-response resource-exhaustion vector.
            // Not advertised. Ceiling (Min): a child may only shrink it.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.SectorIdentifierUriMaxSizeBytes,
                Rule = SettingRules.AtLeast(1),
                Default = 100 * 1024,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // pairwise_salt: a stable, secret per-tenant salt mixed into the pairwise subject derivation to harden it
            // against a guessed-principal preimage attack (OpenID Connect Core 1.0 §8.1). Derivation stays deterministic
            // without it (the tenant id already isolates tenants), but configuring it is recommended. Not advertised.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PairwiseSalt,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // security_profiles: the named security baselines (e.g. fapi2) a scope selects to tighten the OP; empty (the
            // default) selects none, and every value must name a registered profile, because a name that matches none
            // would silently leave the scope without the baseline it asked for. Not advertised. Floor (Union): the
            // profiles accumulate down the cascade, so a child can add a profile but never remove one a parent selects
            // (a server or tenant can mandate a profile), and every selected profile applies. (ADR-0148)
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SecurityProfiles,
                Rule = SecurityProfiles(),
                Default = [],

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Union,
            };

            // subject_types_supported: subject identifier types the server supports (public, pairwise). Both are
            // implemented (F9), so both are advertised by default. Ceiling (Intersect): a child narrows the parent's set.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SubjectTypesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.SubjectType()),
                Default =
                [
                    OpenIdConstants.SubjectTypes.Public,
                    OpenIdConstants.SubjectTypes.Pairwise,
                ],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // token_endpoint_auth_signing_alg_values_supported: permitted "alg" values for private_key_jwt / client_secret_jwt client auth.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,
                Rule = SignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // ui_locales_supported: BCP47 locales the server's UI supports.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UiLocalesSupported,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_alg_values_supported: permitted "alg" values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported,
                Rule = KeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_enc_values_supported: permitted "enc" values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionEncValuesSupported,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_zip_values_supported: permitted "zip" compression values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionZipValuesSupported,
                Rule = CompressionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_signing_alg_values_supported: permitted "alg" values for signing the UserInfo response.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoSigningAlgValuesSupported,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };
        }
    }

    /// <summary>
    /// Gets the rule for <c>security_profiles</c>: a list whose every value names a registered
    /// <see cref="ISecurityProfile"/>; the empty list selects no profile.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> SecurityProfiles() =>
        SecurityProfileNames.IsEmpty
            ? SettingRules.Must<IReadOnlyCollection<string>>(
                "empty, because no security profile is registered",
                value => value.Count == 0
            )
            : SettingRules.Each(SettingRules.OneOf(SecurityProfileNames));

    /// <summary>
    /// Gets the rule for a list of signature algorithms a client or the server may choose from, symmetric ones
    /// included: every value names a registered signature algorithm.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> SignatureAlgorithms() =>
        RegisteredAlgorithms(
            AlgorithmType.DigitalSignature,
            "a registered signature algorithm such as 'ES256'"
        );

    /// <summary>
    /// Gets the rule for a list of algorithms that sign a token crossing a trust boundary (an ID token, a UserInfo
    /// response, an access token, a DPoP proof): every value names a registered asymmetric signature algorithm, never a
    /// symmetric one.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> AsymmetricSignatureAlgorithms() =>
        SettingRules.Each(
            SettingRules.Must<string>(
                "a registered asymmetric signature algorithm such as 'ES256'",
                code =>
                    TryGetSelectableAlgorithm(
                        AlgorithmType.DigitalSignature,
                        code,
                        out var algorithm
                    ) && SettingAlgorithms.IsAsymmetricSignature(algorithm)
            )
        );

    /// <summary>
    /// Gets the rule for a list of key management (<c>alg</c>) algorithms for encryption.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> KeyManagementAlgorithms() =>
        RegisteredAlgorithms(
            AlgorithmType.KeyManagement,
            "a registered key management algorithm such as 'RSA-OAEP-256'"
        );

    /// <summary>
    /// Gets the rule for a list of content encryption (<c>enc</c>) algorithms.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> ContentEncryptionAlgorithms() =>
        RegisteredAlgorithms(
            AlgorithmType.AuthenticatedEncryption,
            "a registered content encryption algorithm such as 'A256GCM'"
        );

    /// <summary>
    /// Gets the rule for a list of compression (<c>zip</c>) algorithms.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> CompressionAlgorithms() =>
        RegisteredAlgorithms(
            AlgorithmType.Compression,
            "a registered compression algorithm such as 'DEF'"
        );

    // The rule consults the algorithm collection each time it validates, so it follows the registered algorithms as
    // they change.
    private SettingRule<IReadOnlyCollection<string>> RegisteredAlgorithms(
        AlgorithmType algorithmType,
        string description
    ) =>
        SettingRules.Each(
            SettingRules.Must<string>(
                description,
                code => TryGetSelectableAlgorithm(algorithmType, code, out _)
            )
        );

    private bool TryGetSelectableAlgorithm(
        AlgorithmType algorithmType,
        string code,
        [NotNullWhen(true)] out Algorithm? algorithm
    ) =>
        AlgorithmCollectionProvider.Collection.TryGetAlgorithm<Algorithm>(
            algorithmType,
            code,
            out algorithm
        ) && SettingAlgorithms.IsSelectable(algorithm);
}
