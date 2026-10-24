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
/// <para>
/// Every descriptor declares its domain through <see cref="SettingDescriptor{TValue}.Rule"/>. A rule whose domain is
/// defined by registered strategies consults them: an algorithm setting accepts the registered algorithms of its kind,
/// and <c>security_profiles</c> accepts the names of the registered <see cref="ISecurityProfile"/> implementations.
/// </para>
/// <para>
/// Every descriptor also declares the scopes it applies to through <see cref="SettingDescriptor.AppliesTo"/>: the
/// scopes whose effective settings the server reads it from. Discovery and the tenant-wide services (sessions,
/// principal resolution, federation, dynamic registration policy) read a tenant's effective settings, so every
/// discoverable setting applies to tenants; the protocol endpoints read the effective settings of the client making the
/// request; and access-token issuance combines the client's effective settings with those of every resource server the
/// token targets, so every setting that applies to resource servers merges with a lattice operation (ADR-0158).
/// </para>
/// </remarks>
// See ADR-0140 (domains), ADR-0149 (scopes), and ADR-0158 (resource-server bounds).
internal class DefaultSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IEnumerable<ISecurityProfile> securityProfiles,
    IEnumerable<IRedirectUriKind> redirectUriKinds
) : ICollectionDataSource<SettingDescriptor>
{
    private const bool IsStdDiscoverable = true;
    private const bool IsNonStdDiscoverable = false;

    private static ImmutableArray<string> Tenants { get; } = [OpenIdSettingScopes.Tenant];
    private static ImmutableArray<string> Clients { get; } = [OpenIdSettingScopes.Client];
    private static ImmutableArray<string> ResourceServers { get; } =
    [OpenIdSettingScopes.ResourceServer];

    private static ImmutableArray<string> TenantsAndClients { get; } =
    [OpenIdSettingScopes.Tenant, OpenIdSettingScopes.Client];

    private static ImmutableArray<string> ClientsAndResourceServers { get; } =
    [OpenIdSettingScopes.Client, OpenIdSettingScopes.ResourceServer];

    // RFC 8628 §6.1: eight characters from the 20-character default alphabet give about 34.5 bits of entropy, the
    // floor that keeps a user_code impractical to guess under rate limiting; the ceiling keeps it typeable.
    private const int MinUserCodeLength = 8;
    private const int MaxUserCodeLength = 32;

    private const string RetiredMtlsEndpointAliasAuthority = "mtls_endpoint_alias_authority";

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ImmutableArray<string> SecurityProfileNames { get; } =
    [.. securityProfiles.Select(profile => profile.ProfileName).Order(StringComparer.Ordinal)];

    private ImmutableArray<string> RedirectUriKindNames { get; } =
    [.. redirectUriKinds.Select(kind => kind.KindName).Order(StringComparer.Ordinal)];

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
                AppliesTo = Tenants,
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
                AppliesTo = Tenants,
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
                AppliesTo = Tenants,
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
                AppliesTo = Tenants,
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
                AppliesTo = Tenants,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // federated_identity_explicit_only: only link federated identities that were explicitly provisioned (default false). (ADR-0035)
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FederatedIdentityExplicitOnly,
                AppliesTo = Tenants,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // access_token_confirmation_methods_supported: the 'cnf' methods a resource server verifies (jkt for DPoP,
            // x5t#S256 for mutual TLS). A required sender constraint is met only by a binding every targeted resource
            // server verifies. Ceiling (Intersect): a child narrows the parent's set; unset = every method (ADR-0010).
            // Scopes: a resource server's bound on the tokens issued for it, combined at issuance (ADR-0158).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenConfirmationMethodsSupported,
                AppliesTo = ResourceServers,
                Rule = SettingRules.Each(OpenIdSettingRules.ConfirmationMethod()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_alg_values_supported: permitted "alg" values for access-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            // Scopes: the client's and every targeted resource server's lists combine at issuance (ADR-0158).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported,
                AppliesTo = ClientsAndResourceServers,
                Rule = KeyManagementAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_enc_values_supported: permitted "enc" values for access-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            // Scopes: the client's and every targeted resource server's lists combine at issuance (ADR-0158).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported,
                AppliesTo = ClientsAndResourceServers,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_required: whether issued access tokens must be encrypted (default false).
            // Floor (Or): once a parent requires it, a child cannot un-require it. Scopes: the client's and every
            // targeted resource server's requirements combine at issuance (ADR-0075, ADR-0158).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionRequired,
                AppliesTo = ClientsAndResourceServers,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // access_token_lifetime: validity window for issued access tokens (default 5 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            // Scopes: the client's and every targeted resource server's lifetimes combine at issuance (ADR-0158).
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AccessTokenLifetime,
                AppliesTo = ClientsAndResourceServers,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // access_token_sender_constraint_required: whether an access token must be bound to a DPoP proof key or a
            // mutual-TLS client certificate (default false). Floor (Or): once a parent requires it, a child cannot
            // un-require it. Scopes: the client's requirement is enforced at the token endpoint; the client's and every
            // targeted resource server's requirements combine at issuance (ADR-0158).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AccessTokenSenderConstraintRequired,
                AppliesTo = ClientsAndResourceServers,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // access_token_signing_alg_values_supported: permitted "alg" values for access-token signing.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            // Scopes: the client's and every targeted resource server's lists combine at issuance (ADR-0158).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenSigningAlgValuesSupported,
                AppliesTo = ClientsAndResourceServers,
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
                AppliesTo = Clients,
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
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // default_acr: the baseline acr value asserted for a single-factor authentication event (OP behavior, not
            // advertised). Replace: a child's value overrides the parent's. Scopes: an authentication event belongs to
            // the tenant's session, not to one client, so the tenant's settings decide it.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.DefaultAcr,
                AppliesTo = Tenants,
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
                AppliesTo = Clients,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allow_loopback_redirect: whether a registered loopback IP redirect URI (http://127.0.0.1 or http://[::1])
            // may be requested with any port (RFC 8252 §7.3). Every other part of a redirect URI must equal a registered
            // one exactly. Default true; fapi1-advanced turns it off (ADR-0203).
            // Ceiling (And): once a parent forbids it, a child cannot re-allow it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowLoopbackRedirect,
                AppliesTo = Clients,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allowed_identity_providers: identity providers a client may use; unset permits any.
            // Ceiling (Intersect): a child narrows the parent's set; a set (or narrowed-to-empty) list denies all others (fail-closed).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AllowedIdentityProviders,
                AppliesTo = Clients,
                Rule = SettingRules.Each(SettingRules.NonEmpty()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // authorization_authenticate_scheme: ASP.NET Core auth scheme used to authenticate the end-user (default application cookie).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationAuthenticateScheme,
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Tenants,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // claims_locales_supported: BCP47 locales for which the server can return localized claim values.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsLocalesSupported,
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // claims_parameter_supported: whether the OIDC "claims" request parameter is honored (default true; ADR-0039).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsParameterSupported,
                AppliesTo = TenantsAndClients,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // claims_supported: claim names the OP may return (advertised in discovery; default supplied by the baseline provider).
            // Override (Replace). Enforced as an allow-list only when claims_supported_is_strict is true.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsSupported,
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // claims_supported_is_strict: when true, emitted claims are filtered to claims_supported (default false).
            // Floor (Or): once a parent enables strict filtering, a child cannot disable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsSupportedIsStrict,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // claim_types_supported: OIDC claim types the server can emit (default "normal"; aggregated/distributed are not implemented).
            // Ceiling (Intersect): advisory discovery only (no runtime consumer); reflects a fixed capability.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimTypesSupported,
                AppliesTo = Tenants,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.BackchannelTokenDeliveryMode()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // backchannel_user_code_parameter_supported: whether the server supports the CIBA user_code parameter (default false);
            // a client's registered backchannel_user_code_parameter is stored here, at the client's scope.
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackchannelUserCodeParameterSupported,
                AppliesTo = TenantsAndClients,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // backchannel_authentication_request_signing_alg_values_supported: permitted "alg" values for a signed CIBA request object.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010). CIBA Core §7.1.1
            // requires an asymmetric signature (and defines no encrypted request), so the domain excludes symmetric
            // algorithms; enforced against the requesting client's effective settings (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.BackchannelAuthenticationRequestSigningAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // ciba_request_lifetime: validity window for a pending CIBA request (default 10 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.CibaRequestLifetime,
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.DisplayValue()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // grant_types_supported: OAuth grant types the server permits (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; capability is enforced by grant handlers (ADR-0010/0026).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.GrantTypesSupported,
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.GrantType()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encrypted_response_alg: the key-management "alg" the client registers for encrypting its ID tokens
            // to its public key (OpenID Connect Dynamic Client Registration 1.0 §2); absent = ID tokens are not encrypted.
            // Override (Replace): a child fully replaces the parent value. The effective
            // id_token_encryption_alg_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.IdTokenEncryptedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricKeyManagementAlgorithm(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // id_token_encrypted_response_enc: the content-encryption "enc" of the client's encrypted ID tokens
            // (OpenID Connect Dynamic Client Registration 1.0 §2: A128CBC-HS256 unless registered).
            // Override (Replace): a child fully replaces the parent value. The effective
            // id_token_encryption_enc_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.IdTokenEncryptedResponseEnc,
                AppliesTo = Clients,
                Rule = ContentEncryptionAlgorithm(),
                Default = AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // id_token_encryption_alg_values_supported: permitted "alg" values for id-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010). An ID token is
            // encrypted to the client's public key, so only asymmetric key management applies (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricKeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_enc_values_supported: permitted "enc" values for id-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionEncValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_required: whether issued id tokens must be encrypted (default false); a client that
            // registers no id_token_encrypted_response_alg then cannot be issued an ID token (ADR-0155).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionRequired,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // id_token_lifetime: validity window for issued id tokens (default 5 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.IdTokenLifetime,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // id_token_signed_response_alg: the signature "alg" the client registers for its ID tokens (OpenID Connect
            // Dynamic Client Registration 1.0 §2: RS256 unless registered); also signs its logout tokens.
            // Override (Replace): a child fully replaces the parent value, so a server or tenant sets the default for
            // its clients. The effective id_token_signing_alg_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.IdTokenSignedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricSignatureAlgorithm(),
                Default = AlgorithmCodes.DigitalSignature.RsaSha256,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // id_token_signing_alg_values_supported: permitted "alg" values for id-token signing.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenSigningAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // op_policy_uri: URL of the OP's data-usage policy document.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderPolicyUri,
                AppliesTo = Tenants,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // op_tos_uri: URL of the OP's terms-of-service document.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderTermsOfServiceUri,
                AppliesTo = Tenants,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // prompt_values_supported: OIDC "prompt" values the server supports (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.PromptValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.PromptValue()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // redirect_uris: the registered redirect URIs for a client.
            // Override (Replace): each scope specifies its own set (per-client registration, not a cascade ceiling).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RedirectUris,
                AppliesTo = Clients,
                Rule = SettingRules.Each(OpenIdSettingRules.RedirectUri()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // redirect_uri_kinds_supported: the kinds of redirect URI (https, loopback, http, private_use) a client may
            // register and use; unset permits every registered kind, and a security profile caps it (ADR-0203). Not
            // advertised. Ceiling (Intersect): a child narrows the parent's set.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RedirectUriKindsSupported,
                AppliesTo = TenantsAndClients,
                Rule = RegisteredRedirectUriKinds(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // post_logout_redirect_uris: the registered post-logout redirect URIs for a client (RP-Initiated Logout).
            // Override (Replace): each client specifies its own set (per-client registration, not a cascade ceiling).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.PostLogoutRedirectUris,
                AppliesTo = Clients,
                Rule = SettingRules.Each(OpenIdSettingRules.RedirectUri()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_uri: the per-client URI the OP POSTs the logout_token to (Back-Channel Logout).
            // Override (Replace): each client specifies its own endpoint.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.BackChannelLogoutUri,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.LogoutUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_session_required: whether the client requires a sid in the logout_token.
            // Override (Replace): each client specifies its own requirement.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackChannelLogoutSessionRequired,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_supported: server advertises Back-Channel Logout support (default true).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackChannelLogoutSupported,
                AppliesTo = Tenants,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // backchannel_logout_session_supported: server advertises that logout_tokens include a sid (default true).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.BackChannelLogoutSessionSupported,
                AppliesTo = Tenants,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_uri: the per-client URI the OP loads in a hidden iframe (Front-Channel Logout).
            // Override (Replace): each client specifies its own endpoint.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutUri,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.LogoutUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_session_required: whether the OP appends iss/sid to the client's frontchannel_logout_uri.
            // Override (Replace): each client specifies its own requirement.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutSessionRequired,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_supported: server advertises Front-Channel Logout support (default false — the
            // mechanism is available but disabled by default because it depends on third-party cookies). (ADR-0088)
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutSupported,
                AppliesTo = Tenants,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // frontchannel_logout_session_supported: server advertises that it appends iss/sid to front-channel
            // logout URIs (default false).
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FrontChannelLogoutSessionSupported,
                AppliesTo = Tenants,
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
                AppliesTo = Tenants,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // session_absolute_lifetime: absolute lifetime of the server-side end-user session, after which it ends
            // however it is used. Between one second and 365 days; default 30 days (Auth0's tenant default and NIST
            // SP 800-63B's AAL1 reauthentication of at least once per 30 days; the maximum is Auth0's). Not advertised
            // (leaking session policy aids attackers). Override (Replace): a tenant fully replaces the value.
            // (ADR-0220)
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SessionAbsoluteLifetime,
                AppliesTo = Tenants,
                Rule = SettingRules.Between(TimeSpan.FromSeconds(1), TimeSpan.FromDays(365)),
                Default = TimeSpan.FromDays(30),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // session_idle_timeout: sliding inactivity window after which the end-user session ends if unused; each
            // authorize request that resolves the session renews it. Between one second and 100 days; default 3 days
            // (Auth0's inactivity default; the maximum is Auth0's enterprise maximum). Not advertised. Override
            // (Replace): a tenant fully replaces the value. (ADR-0220)
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SessionIdleTimeout,
                AppliesTo = Tenants,
                Rule = SettingRules.Between(TimeSpan.FromSeconds(1), TimeSpan.FromDays(100)),
                Default = TimeSpan.FromDays(3),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // max_concurrent_sessions: cap on the number of simultaneous sessions a subject may hold; the oldest beyond
            // the cap are revoked at sign-in. 0 disables the limit. Default 0. Not advertised. Override (Replace): a
            // tenant fully replaces the value. (F25)
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.MaxConcurrentSessions,
                AppliesTo = Tenants,
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
                AppliesTo = Tenants,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.RefreshTokenReusePolicy(),
                Default = OpenIdConstants.RefreshTokenReusePolicy.RevokeAll,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_rotation_policy: which refresh tokens are rotated, that is replaced by a new refresh token
            // each time they are used — unbound (the default) rotates a token bound neither to a proof-of-possession
            // key nor to client authentication, that is a public client's token without a DPoP or certificate binding,
            // as RFC 9700 §2.2.2 and §4.14.2 require; always rotates every refresh token. No value turns off the
            // rotation of a public client's unbound token. (ADR-0216)
            // Override (Replace): a child fully replaces the parent value, so a tenant cannot force rotation on its
            // clients; the floor that matters is the requirement that a public client's unbound token is rotated.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RefreshTokenRotationPolicy,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.RefreshTokenRotationPolicy(),
                Default = OpenIdConstants.RefreshTokenRotationPolicy.Unbound,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_reuse_interval: how long after a rotation the rotated refresh token may be presented again
            // as a retry instead of a reuse, so a client that lost the rotation response can recover; the retry is
            // answered as the rotation was and the rotation family is not revoked. TimeSpan.Zero turns it off
            // (default); at most one minute, because a longer window weakens reuse detection. (ADR-0217)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RefreshTokenReuseInterval,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.RefreshTokenReuseInterval(),
                Default = TimeSpan.Zero,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_rotation_retry_required: whether a client whose refresh tokens rotate must be able to retry
            // a rotation, that is have a positive refresh_token_reuse_interval (default false; FAPI 2.0 §5.3.2.1
            // requires it).
            // Floor (Or): once a parent requires it, a child cannot un-require it. (ADR-0206, ADR-0217)
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RefreshTokenRotationRetryRequired,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // refresh_token_lifetime: validity window for issued refresh tokens (default 30 days).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RefreshTokenLifetime,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromDays(30.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // request_object_encryption_alg_values_supported: permitted "alg" values for encrypting request objects.
            // Ceiling (Intersect): a child narrows the parent's set. A request object is encrypted to the server's public
            // key, so only asymmetric key management applies; advertised from the tenant's settings and enforced against
            // the requesting client's before the request object is decrypted, where an unset list permits nothing
            // (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricKeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_encryption_enc_values_supported: permitted "enc" values for encrypting request objects.
            // Ceiling (Intersect): a child narrows the parent's set. Advertised from the tenant's settings and enforced
            // against the requesting client's before the request object is decrypted, where an unset list permits nothing
            // (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_signing_alg_values_supported: permitted "alg" values for signing request objects.
            // Ceiling (Intersect): a child narrows the parent's set. Advertised from the tenant's settings and enforced
            // against the requesting client's before the signature is verified, where an unset list permits nothing; a
            // client that registers a request_object_signing_alg narrows its own list to it (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = SignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_expected_audience: expected "aud" of a client's request object; empty expects the tenant's
            // issuer (RFC 9101 §4, ADR-0187), which a FAPI profile requires (ADR-0202) (default empty).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestObjectExpectedAudience,
                AppliesTo = Clients,
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // request_object_max_lifetime: longest lifetime accepted for a signed request object or CIBA authentication
            // request, from its earliest 'iat'/'nbf' to its 'exp' (default 60 minutes, FAPI's bound; ADR-0187).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RequestObjectMaxLifetime,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.Lifetime(),
                Default = TimeSpan.FromMinutes(60),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // request_object_nbf_required: whether a signed request object must carry 'nbf' (default false; FAPI requires
            // it; a CIBA authentication request always must, ADR-0187).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestObjectNotBeforeRequired,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // request_parameter_supported: whether a passed-by-value "request" object parameter is accepted (default true).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestParameterSupported,
                AppliesTo = TenantsAndClients,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // request_uri_parameter_supported: whether a "request_uri" parameter is accepted (default true).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriParameterSupported,
                AppliesTo = TenantsAndClients,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // request_uri_require_strict_content_type: require the request_uri response to use the expected content type (default false).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriRequireStrictContentType,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // request_uri_expected_content_type: expected content type when fetching a request_uri (default application/oauth-authz-req+jwt).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestUriExpectedContentType,
                AppliesTo = Clients,
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
                AppliesTo = Clients,
                Rule = SettingRules.Each(OpenIdSettingRules.HostName()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_uri_fetch_timeout: wall-clock cap on the JAR request_uri fetch (RFC 9101). Not advertised.
            // Ceiling (Min): a child may only shorten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RequestUriFetchTimeout,
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // require_request_uri_registration: whether request_uri values must be pre-registered by the client.
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireRequestUriRegistration,
                AppliesTo = Tenants,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // require_pushed_authorization_requests: whether the client must push its authorization request to the PAR
            // endpoint before invoking the authorization endpoint (default false; RFC 9126 §5).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequirePushedAuthorizationRequests,
                AppliesTo = TenantsAndClients,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // pushed_authorization_request_lifetime: validity window of a pushed authorization request (default 90
            // seconds; RFC 9126 §2.2). Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.PushedAuthorizationRequestLifetime,
                AppliesTo = Clients,
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
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // dpop_proof_lifetime: acceptable age window of a DPoP proof's iat (RFC 9449 §4.3), default 60 seconds.
            // Ceiling (Min): a child may only tighten it. Scopes: the token endpoint validates a proof with the client's
            // settings and the UserInfo endpoint with the tenant's.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.DPoPProofLifetime,
                AppliesTo = TenantsAndClients,
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
                AppliesTo = TenantsAndClients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // require_dpop_bound_access_tokens: whether the client must present a valid DPoP proof at the token
            // endpoint (RFC 9449), default false. Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireDPoPBoundAccessTokens,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // tls_client_certificate_bound_access_tokens: whether the server supports mutual-TLS
            // client-certificate-bound access tokens, which discovery advertises (RFC 8705 §3.3); it never requires a
            // client to bind. Secure-by-default false (only advertise once the deployment has wired certificate
            // forwarding). Override (Replace): a tenant overrides the server.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.TlsClientCertificateBoundAccessTokens,
                AppliesTo = Tenants,
                Default = false,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // require_tls_client_certificate_bound_access_tokens: whether the client's access tokens must be bound to
            // the mutual-TLS certificate it presents (RFC 8705 §3), default false. A server or tenant value is a floor
            // for every client. Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens,
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // tls_client_auth_trust_anchors: the PEM CA certificates a tls_client_auth certificate must chain to; a
            // self-issued certificate is an anchor, any other an intermediate. Empty (the default) trusts no
            // certificate, so tls_client_auth fails closed until a tenant or client configures its PKI. Not
            // advertised. Override (Replace): a client pins its own PKI in place of the tenant's.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TlsClientAuthTrustAnchors,
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.CaCertificatePem()),
                Default = [],

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // tls_client_auth_revocation_mode: how a tls_client_auth chain's revocation status is checked; the secure
            // default is online, where the server fetches each issuer's CRL through its guarded outbound HTTP client
            // and rejects a chain whose status cannot be determined. online_soft_fail refuses a revoked certificate
            // and accepts one whose status cannot be determined. Not advertised. Override (Replace): a child fully
            // replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthRevocationMode,
                AppliesTo = TenantsAndClients,
                Rule = OpenIdSettingRules.ClientCertificateRevocationMode(),
                Default = OpenIdConstants.ClientCertificateRevocationMode.Online,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // tls_client_auth_subject_dn / _san_dns / _san_uri / _san_ip / _san_email: the expected certificate identity
            // a PKI-validated tls_client_auth client must present (RFC 8705 §2.1.2); exactly one is configured per
            // client. Not advertised. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSubjectDn,
                AppliesTo = Clients,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.DistinguishedName()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanDns,
                AppliesTo = Clients,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.DnsName()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanUri,
                AppliesTo = Clients,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.AbsoluteUri()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanIp,
                AppliesTo = Clients,
                Rule = SettingRules.EmptyOr(OpenIdSettingRules.IpAddress()),
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TlsClientAuthSanEmail,
                AppliesTo = Clients,
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
                AppliesTo = TenantsAndClients,
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
                AppliesTo = TenantsAndClients,
                Rule = SettingRules.Each(OpenIdSettingRules.ResponseType()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // send_id_claims_in_access_token: also place id-token claims into the access token (default false).
            // Override (Replace): behavioral toggle; a child fully replaces the parent value.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendIdClaimsInAccessToken,
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // send_profile_metadata_in_user_info: project the account's owner-updatable profile metadata bag into the
            // UserInfo response as a single JSON object claim (default false). Override (Replace): a child fully
            // replaces the parent value. Scopes: the UserInfo endpoint reads the tenant's settings, not a client's.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendProfileMetadataInUserInfo,
                AppliesTo = Tenants,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Tenants,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // service_documentation: URL of human-readable developer documentation for the server.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ServiceDocumentation,
                AppliesTo = Tenants,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // subject_max_age: maximum age of the end-user authentication a client will accept. Has no default.
            // Ceiling (Min): a child may only shorten it; a shorter value demands fresher authentication.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SubjectMaxAge,
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.HttpsUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // RFC 7591 §2 client metadata: client_name, client_uri, logo_uri, tos_uri, policy_uri, contacts, software_id,
            // software_version. Registered so a registration (or the management API) stores them as typed settings rather
            // than untyped extras; client_name is shown on the consent page. The jwks metadata is deliberately not a
            // setting: its keys are the client's stored public keys, imported under the client-key policy, and a
            // registration response projects them back (ADR-0161). No defaults: absent means the client did not provide
            // it. Not advertised. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ClientName,
                AppliesTo = Clients,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ClientUri,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.LogoUri,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TosUri,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PolicyUri,
                AppliesTo = Clients,
                Rule = OpenIdSettingRules.WebUri(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.Contacts,
                AppliesTo = Clients,
                Rule = SettingRules.Each(SettingRules.NonEmpty()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SoftwareId,
                AppliesTo = Clients,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SoftwareVersion,
                AppliesTo = Clients,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // jwks_uri_allowed_hosts: SSRF allow-list for the runtime client jwks_uri fetch. When set, the jwks_uri
            // host must appear in this list; unset = no host allow-list, though the always-on private/loopback/
            // link-local address block still applies. Not advertised. Ceiling (Intersect): a child narrows the parent.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.JwksUriAllowedHosts,
                AppliesTo = Clients,
                Rule = SettingRules.Each(OpenIdSettingRules.HostName()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // jwks_uri_fetch_timeout: wall-clock cap on the runtime client jwks_uri fetch. Not advertised.
            // Ceiling (Min): a child may only shorten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.JwksUriFetchTimeout,
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Clients,
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
                AppliesTo = Tenants,
                Rule = SettingRules.Each(OpenIdSettingRules.HostName()),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // sector_identifier_uri_fetch_timeout: wall-clock cap on the registration-time sector_identifier_uri fetch.
            // Not advertised. Ceiling (Min): a child may only shorten it.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SectorIdentifierUriFetchTimeout,
                AppliesTo = Tenants,
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
                AppliesTo = Tenants,
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
                AppliesTo = Clients,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // security_profiles: the named security baselines (e.g. fapi2) a scope selects to tighten the OP; empty (the
            // default) selects none, and every value must name a registered profile, because a name that matches none
            // would silently leave the scope without the baseline it asked for. Not advertised. Floor (Union): the
            // profiles accumulate down the cascade, so a child can add a profile but never remove one a parent selects
            // (a server or tenant can mandate a profile), and every selected profile applies. (ADR-0148)
            // Scopes: a tenant's selection expands into the ceilings discovery advertises, and a client's effective
            // selection is enforced on its requests; a resource server selects none, since no profile governs one. (ADR-0149)
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SecurityProfiles,
                AppliesTo = TenantsAndClients,
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
                AppliesTo = Tenants,
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
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010). Scopes: advertised
            // from the tenant's settings and enforced against the authenticating client's.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = SignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // ui_locales_supported: BCP47 locales the server's UI supports.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UiLocalesSupported,
                AppliesTo = Tenants,
                Rule = SettingRules.Each(OpenIdSettingRules.Token()),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encrypted_response_alg: the key-management "alg" the client registers for encrypting its UserInfo
            // responses to its public key (OpenID Connect Dynamic Client Registration 1.0 §2); absent = not encrypted.
            // Override (Replace): a child fully replaces the parent value. The effective
            // userinfo_encryption_alg_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.UserInfoEncryptedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricKeyManagementAlgorithm(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // userinfo_encrypted_response_enc: the content-encryption "enc" of the client's encrypted UserInfo responses
            // (OpenID Connect Dynamic Client Registration 1.0 §2: A128CBC-HS256 unless registered).
            // Override (Replace): a child fully replaces the parent value. The effective
            // userinfo_encryption_enc_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.UserInfoEncryptedResponseEnc,
                AppliesTo = Clients,
                Rule = ContentEncryptionAlgorithm(),
                Default = AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // userinfo_encryption_alg_values_supported: permitted "alg" values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010). A UserInfo response
            // is encrypted to the client's public key, so only asymmetric key management applies; advertised from the
            // tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricKeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_enc_values_supported: permitted "enc" values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010). Advertised from the
            // tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionEncValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_signed_response_alg: the signature "alg" the client registers for its UserInfo responses (OpenID
            // Connect Dynamic Client Registration 1.0 §2); absent = the response is not signed.
            // Override (Replace): a child fully replaces the parent value. The effective
            // userinfo_signing_alg_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.UserInfoSignedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricSignatureAlgorithm(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // userinfo_signing_alg_values_supported: permitted "alg" values for signing the UserInfo response.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010). Advertised from the
            // tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoSigningAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // introspection_signed_response_alg: the signature "alg" a client acting for a resource server registers for
            // its JWT introspection responses (RFC 9701 §6: RS256 unless registered).
            // Override (Replace): a child fully replaces the parent value. The effective
            // introspection_signing_alg_values_supported must permit it (ADR-0155, ADR-0168).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.IntrospectionSignedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricSignatureAlgorithm(),
                Default = AlgorithmCodes.DigitalSignature.RsaSha256,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // introspection_signing_alg_values_supported: permitted "alg" values for signing a JWT introspection
            // response (RFC 9701 §7). Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted
            // (ADR-0010). Advertised from the tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IntrospectionSigningAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // introspection_encrypted_response_alg: the key-management "alg" a client acting for a resource server
            // registers for encrypting its JWT introspection responses to its public key (RFC 9701 §6); absent = not
            // encrypted. Override (Replace): a child fully replaces the parent value. The effective
            // introspection_encryption_alg_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.IntrospectionEncryptedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricKeyManagementAlgorithm(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // introspection_encrypted_response_enc: the content-encryption "enc" of the client's encrypted JWT
            // introspection responses (RFC 9701 §6: A128CBC-HS256 unless registered).
            // Override (Replace): a child fully replaces the parent value. The effective
            // introspection_encryption_enc_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.IntrospectionEncryptedResponseEnc,
                AppliesTo = Clients,
                Rule = ContentEncryptionAlgorithm(),
                Default = AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // introspection_encryption_alg_values_supported: permitted "alg" values for encrypting a JWT introspection
            // response (RFC 9701 §7). Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted
            // (ADR-0010). The response is encrypted to the client's public key, so only asymmetric key management
            // applies; advertised from the tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IntrospectionEncryptionAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricKeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // introspection_encryption_enc_values_supported: permitted "enc" values for encrypting a JWT introspection
            // response (RFC 9701 §7). Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted
            // (ADR-0010). Advertised from the tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IntrospectionEncryptionEncValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // authorization_signed_response_alg: the signature "alg" a client registers for
            // its JWT-secured authorization responses (JARM) (JARM §3: RS256 unless registered).
            // Override (Replace): a child fully replaces the parent value. The effective
            // authorization_signing_alg_values_supported must permit it (ADR-0155, ADR-0168).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationSignedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricSignatureAlgorithm(),
                Default = AlgorithmCodes.DigitalSignature.RsaSha256,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // authorization_response_lifetime: lifetime of a JARM response JWT; JARM §2.1 recommends at most 10 minutes. Ceiling (Min).
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AuthorizationResponseLifetime,
                AppliesTo = Clients,
                Rule = SettingRules.Between(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(10)),
                Default = TimeSpan.FromMinutes(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // authorization_signing_alg_values_supported: permitted "alg" values for signing a JWT-secured authorization
            // response (JARM §4); a key-provisioning default (ADR-0169). Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted
            // (ADR-0010). Advertised from the tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AuthorizationSigningAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricSignatureAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // authorization_encrypted_response_alg: the key-management "alg" a client acting for a resource server
            // registers for encrypting its JWT-secured authorization responses (JARM) to its public key (JARM §3); absent = not
            // encrypted. Override (Replace): a child fully replaces the parent value. The effective
            // authorization_encryption_alg_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationEncryptedResponseAlg,
                AppliesTo = Clients,
                Rule = AsymmetricKeyManagementAlgorithm(),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // authorization_encrypted_response_enc: the content-encryption "enc" of the client's encrypted JWT
            // introspection responses (JARM §3: A128CBC-HS256 unless registered).
            // Override (Replace): a child fully replaces the parent value. The effective
            // authorization_encryption_enc_values_supported must permit it (ADR-0155).
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationEncryptedResponseEnc,
                AppliesTo = Clients,
                Rule = ContentEncryptionAlgorithm(),
                Default = AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // authorization_encryption_alg_values_supported: permitted "alg" values for encrypting a JWT introspection
            // response (JARM §4). Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted
            // (ADR-0010). The response is encrypted to the client's public key, so only asymmetric key management
            // applies; advertised from the tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AuthorizationEncryptionAlgValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = AsymmetricKeyManagementAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // authorization_encryption_enc_values_supported: permitted "enc" values for encrypting a JWT introspection
            // response (JARM §4). Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted
            // (ADR-0010). Advertised from the tenant's settings and enforced against the client's (ADR-0155).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AuthorizationEncryptionEncValuesSupported,
                AppliesTo = TenantsAndClients,
                Rule = ContentEncryptionAlgorithms(),

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // mtls_endpoint_alias_authority: retired. A tenant's mTLS endpoint alias is a tenant property; the
            // descriptor stays registered so a stored document that carries the setting still loads, until a later
            // release removes the setting from the stored documents (ADR-0195, ADR-0251).
            yield return new SettingDescriptor<string>
            {
                Name = RetiredMtlsEndpointAliasAuthority,
                AppliesTo = Tenants,
                RetiredReason =
                    "A tenant's mTLS endpoint alias is its mtlsEndpointAliasAuthority property, which the management API reads and writes.",
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
    /// Gets the rule for <c>redirect_uri_kinds_supported</c>: a list whose every value names a registered
    /// <see cref="IRedirectUriKind"/>; the empty list permits no redirect URI.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> RegisteredRedirectUriKinds() =>
        RedirectUriKindNames.IsEmpty
            ? SettingRules.Must<IReadOnlyCollection<string>>(
                "empty, because no redirect URI kind is registered",
                value => value.Count == 0
            )
            : SettingRules.Each(SettingRules.OneOf(RedirectUriKindNames));

    /// <summary>
    /// Gets the rule for a list of signature algorithms a client or the server may choose from, symmetric ones
    /// included: every value names a registered signature algorithm.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> SignatureAlgorithms() =>
        SettingRules.Each(
            SelectableAlgorithm(
                AlgorithmType.DigitalSignature,
                "a registered signature algorithm such as 'ES256'"
            )
        );

    /// <summary>
    /// Gets the rule for an algorithm that signs a token crossing a trust boundary (an ID token, a UserInfo response, an
    /// access token, a DPoP proof): the value names a registered asymmetric signature algorithm, never a symmetric one.
    /// </summary>
    private SettingRule<string> AsymmetricSignatureAlgorithm() =>
        SettingRules.Must<string>(
            "a registered asymmetric signature algorithm such as 'ES256'",
            code =>
                TryGetSelectableAlgorithm(AlgorithmType.DigitalSignature, code, out var algorithm)
                && SettingAlgorithms.IsAsymmetricSignature(algorithm)
        );

    /// <summary>
    /// Gets the rule for a list of algorithms that sign a token crossing a trust boundary: every value satisfies
    /// <see cref="AsymmetricSignatureAlgorithm"/>.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> AsymmetricSignatureAlgorithms() =>
        SettingRules.Each(AsymmetricSignatureAlgorithm());

    /// <summary>
    /// Gets the rule for a list of key management (<c>alg</c>) algorithms for encryption.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> KeyManagementAlgorithms() =>
        SettingRules.Each(
            SelectableAlgorithm(
                AlgorithmType.KeyManagement,
                "a registered key management algorithm such as 'RSA-OAEP-256'"
            )
        );

    /// <summary>
    /// Gets the rule for a key management (<c>alg</c>) algorithm that encrypts to a party by its public key (a response
    /// addressed to a client, a request object addressed to the server): the value names a registered asymmetric key
    /// management algorithm, never one that needs a shared key.
    /// </summary>
    private SettingRule<string> AsymmetricKeyManagementAlgorithm() =>
        SettingRules.Must<string>(
            "a registered asymmetric key management algorithm such as 'RSA-OAEP-256'",
            code =>
                TryGetSelectableAlgorithm(AlgorithmType.KeyManagement, code, out var algorithm)
                && SettingAlgorithms.IsAsymmetricKeyManagement(algorithm)
        );

    /// <summary>
    /// Gets the rule for a list of key management algorithms that encrypt to a party by its public key: every value
    /// satisfies <see cref="AsymmetricKeyManagementAlgorithm"/>.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> AsymmetricKeyManagementAlgorithms() =>
        SettingRules.Each(AsymmetricKeyManagementAlgorithm());

    /// <summary>
    /// Gets the rule for a content encryption (<c>enc</c>) algorithm.
    /// </summary>
    private SettingRule<string> ContentEncryptionAlgorithm() =>
        SelectableAlgorithm(
            AlgorithmType.AuthenticatedEncryption,
            "a registered content encryption algorithm such as 'A256GCM'"
        );

    /// <summary>
    /// Gets the rule for a list of content encryption (<c>enc</c>) algorithms.
    /// </summary>
    private SettingRule<IReadOnlyCollection<string>> ContentEncryptionAlgorithms() =>
        SettingRules.Each(ContentEncryptionAlgorithm());

    // The rule consults the algorithm collection each time it validates, so it follows the registered algorithms as
    // they change.
    private SettingRule<string> SelectableAlgorithm(
        AlgorithmType algorithmType,
        string description
    ) =>
        SettingRules.Must<string>(
            description,
            code => TryGetSelectableAlgorithm(algorithmType, code, out _)
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
