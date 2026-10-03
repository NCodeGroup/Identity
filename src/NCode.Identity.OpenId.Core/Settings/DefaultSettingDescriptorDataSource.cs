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

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using static NCode.Identity.Settings.SettingMerge;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="SettingDescriptor"/> instances supported by this library.
/// </summary>
internal class DefaultSettingDescriptorDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<SettingDescriptor>
{
    private const bool IsStdDiscoverable = true;
    private const bool IsNonStdDiscoverable = false;

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // principal_source_claim: claim whose value seeds the resolved principal id (default "sub"). (ADR-0035)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PrincipalSourceClaim,
                Default = "sub",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // principal_issuer_claim: claim identifying the upstream issuer of a federated principal (default "iss"). (ADR-0035)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PrincipalIssuerClaim,
                Default = "iss",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_join_claim: claim used to auto-link a federated identity to an existing principal (default "email"). (ADR-0035)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FederatedIdentityJoinClaim,
                Default = "email",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_verified_claim: boolean claim proving the join claim is verified (default "email_verified"). (ADR-0035)
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FederatedIdentityVerifiedClaim,
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

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_enc_values_supported: permitted "enc" values for access-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported,

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

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_lifetime: validity window for issued access tokens (default 5 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AccessTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // access_token_signing_alg_values_supported: permitted "alg" values for access-token signing.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenSigningAlgValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_type: token format for issued access tokens (default JWT).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AccessTokenType,
                Default = JoseTokenTypes.Jwt,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // acr_values_supported: Authentication Context Class Reference values the server honors.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AcrValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
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

            // allow_plain_code_challenge_method: permit the PKCE "plain" code-challenge method (default true).
            // Ceiling (And): once a parent forbids it, a child cannot re-allow it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowPlainCodeChallengeMethod,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allow_unsafe_token_response: permit returning tokens via a query/fragment response (default true).
            // Ceiling (And): once a parent forbids it, a child cannot re-allow it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowUnsafeTokenResponse,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allowed_identity_providers: identity providers a client may use; unset permits any.
            // Ceiling (Intersect): a child narrows the parent's set; a set (or narrowed-to-empty) list denies all others (fail-closed).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AllowedIdentityProviders,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // authorization_authenticate_scheme: ASP.NET Core auth scheme used to authenticate the end-user (default application cookie).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationAuthenticateScheme,

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
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // claims_locales_supported: BCP47 locales for which the server can return localized claim values.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsLocalesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // claims_parameter_supported: whether the OIDC "claims" request parameter is honored (default false; WIP, not yet implemented).
            // Ceiling (And): once a parent disables it, a child cannot re-enable it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsParameterSupported,
                Default = false, // TODO: this is still a WIP

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // claims_supported: claim names the OP may return (advertised in discovery; default supplied by the baseline provider).
            // Override (Replace). Enforced as an allow-list only when claims_supported_is_strict is true.
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsSupported,

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
                Default = [OpenIdConstants.ClaimTypes.Normal],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // clock_skew: allowed leeway when validating token time claims (default 5 minutes).
            // Ceiling (Min): a child may only reduce the tolerance; raise the server value to allow more.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ClockSkew,
                Default = TimeSpan.FromMinutes(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // continue_authorization_lifetime: validity window for a paused ("continue") authorization flow (default 15 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ContinueAuthorizationLifetime,
                Default = TimeSpan.FromMinutes(15),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // display_values_supported: OIDC "display" parameter values the server supports.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.DisplayValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // grant_types_supported: OAuth grant types the server permits (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; capability is enforced by grant handlers (ADR-0010/0026).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.GrantTypesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_alg_values_supported: permitted "alg" values for id-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_enc_values_supported: permitted "enc" values for id-token encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionEncValuesSupported,

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

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_lifetime: validity window for issued id tokens (default 5 minutes).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.IdTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // id_token_signing_alg_values_supported: permitted "alg" values for id-token signing.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // op_policy_uri: URL of the OP's data-usage policy document.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderPolicyUri,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // op_tos_uri: URL of the OP's terms-of-service document.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderTermsOfServiceUri,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // prompt_values_supported: OIDC "prompt" values the server supports (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.PromptValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // redirect_uris: the registered redirect URIs for a client.
            // Override (Replace): each scope specifies its own set (per-client registration, not a cascade ceiling).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RedirectUris,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_expiration_policy: how a refresh token's expiration is computed — absolute or sliding (default absolute).
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RefreshTokenExpirationPolicy,
                Default = OpenIdConstants.RefreshTokenExpirationPolicy.Absolute,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_lifetime: validity window for issued refresh tokens (default 30 days).
            // Ceiling (Min): a child may only shorten it; raise the server value to allow longer.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RefreshTokenLifetime,
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

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_encryption_enc_values_supported: permitted "enc" values for encrypting request objects.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_encryption_zip_values_supported: permitted "zip" compression values for encrypting request objects.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_signing_alg_values_supported: permitted "alg" values for signing request objects.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,

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
                Default = "application/oauth-authz-req+jwt",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // require_pkce: whether the authorization-code flow must use PKCE (default false).
            // Floor (Or): once a parent requires it, a child cannot un-require it.
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireCodeChallenge,
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

            // response_modes_supported: OAuth response modes the server supports (default supplied by the baseline provider).
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseModesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // response_types_supported: OAuth response types the server supports (default supplied by the baseline provider).
            // Ceiling (Intersect); discovery advertises every valid space-delimited combination (OnFormat).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseTypesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
                OnFormat = FormatUniqueCombinations,
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

            // service_documentation: URL of human-readable developer documentation for the server.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ServiceDocumentation,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // subject_max_age: maximum age of the end-user authentication a client will accept. Has no default.
            // Ceiling (Min): a child may only shorten it; a shorter value demands fresher authentication.
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SubjectMaxAge,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // subject_type: the subject identifier type a client uses — public or pairwise.
            // Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SubjectType,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // subject_types_supported: subject identifier types the server supports (public, pairwise).
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SubjectTypesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // tenant_issuer: the tenant's issuer identifier; the tenant base address is used when unset.
            // Parent-owned (Keep): a child scope cannot override it. Not advertised in discovery.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TenantIssuer,

                IsDiscoverable = false,
                OnMerge = Keep,
            };

            // token_endpoint_auth_signing_alg_values_supported: permitted "alg" values for private_key_jwt / client_secret_jwt client auth.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // ui_locales_supported: BCP47 locales the server's UI supports.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UiLocalesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_alg_values_supported: permitted "alg" values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_enc_values_supported: permitted "enc" values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_zip_values_supported: permitted "zip" compression values for UserInfo response encryption.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_signing_alg_values_supported: permitted "alg" values for signing the UserInfo response.
            // Ceiling (Intersect): a child narrows the parent's set; unset = unrestricted (ADR-0010).
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };
        }
    }

    // The number of combinations grows as (2^n - 1) in the input size, so refuse an input large enough to risk a
    // combinatorial blow-up rather than emitting an unbounded (or truncated, and therefore wrong) document.
    private const int MaxCombinationInputCount = 10;

    /// <summary>
    /// Expands a setting's values into every non-empty combination (subset): each combination's members are ordered
    /// deterministically and joined by a single space, and the combinations themselves are ordered by ascending size.
    /// For example <c>[code, id_token, token]</c> yields <c>code</c>, <c>id_token</c>, <c>token</c>,
    /// <c>code id_token</c>, <c>code token</c>, <c>id_token token</c>, <c>code id_token token</c>.
    /// </summary>
    /// <remarks>
    /// Used as the discovery formatter for <c>response_types_supported</c>, whose metadata advertises every valid
    /// space-delimited response-type combination the server accepts. Because the number of combinations grows as
    /// <c>2^n - 1</c>, an input with more than <see cref="MaxCombinationInputCount"/> values is rejected: listing the
    /// values as-is would advertise single response types instead of their combinations (a protocol violation), and
    /// emitting all combinations would be unbounded, so a misconfiguration fails loudly instead.
    /// </remarks>
    /// <param name="setting">The setting whose values are expanded into combinations.</param>
    /// <returns>The distinct space-delimited combinations, ordered by ascending size.</returns>
    /// <exception cref="InvalidOperationException">
    /// The setting has more than <see cref="MaxCombinationInputCount"/> values.
    /// </exception>
    private static List<string> FormatUniqueCombinations(
        Setting<IReadOnlyCollection<string>> setting
    )
    {
        var values = setting.Value;
        if (values.Count > MaxCombinationInputCount)
            throw new InvalidOperationException(
                $"The '{setting.Descriptor.Name}' setting has {values.Count} values, which exceeds the maximum of "
                    + $"{MaxCombinationInputCount} that can be expanded into discovery combinations (the number of "
                    + "combinations grows as 2^n). Reduce the number of configured values."
            );

        return values
            .Order()
            .Aggregate(
                Enumerable.Empty<IReadOnlyCollection<string>>(),
                (acc, value) =>
                    acc.SelectMany(items => new[] { items, items.Append(value).ToArray() })
                        .Append([value]),
                permutations =>
                    permutations
                        .OrderBy(combinations => combinations.Count)
                        .Select(combinations =>
                            string.Join(OpenIdConstants.ParameterSeparatorChar, combinations)
                        )
            )
            .ToList();
    }
}
