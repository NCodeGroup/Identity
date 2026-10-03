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
            // principal_source_claim (ADR-0035)
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PrincipalSourceClaim,
                Default = "sub",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // principal_issuer_claim (ADR-0035)
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.PrincipalIssuerClaim,
                Default = "iss",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_join_claim (ADR-0035)
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FederatedIdentityJoinClaim,
                Default = "email",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_verified_claim (ADR-0035)
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.FederatedIdentityVerifiedClaim,
                Default = "email_verified",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // federated_identity_require_verified (ADR-0035)
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FederatedIdentityRequireVerified,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // federated_identity_explicit_only (ADR-0035)
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.FederatedIdentityExplicitOnly,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // access_token_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_encryption_required
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // access_token_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AccessTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // access_token_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenSigningAlgValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // access_token_type
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AccessTokenType,
                Default = JoseTokenTypes.Jwt,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // acr_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AcrValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // allow_loopback_redirect
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowLoopbackRedirect,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allow_plain_code_challenge_method
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowPlainCodeChallengeMethod,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allow_unsafe_token_response
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowUnsafeTokenResponse,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = And,
            };

            // allowed_identity_providers
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AllowedIdentityProviders,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // authorization_authenticate_scheme
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationAuthenticateScheme,

                // we are compatible with Microsoft.AspNetCore.Identity
                // do not use the "Identity.External" scheme as it is only for external identity providers
                Default = IdentityConstants.ApplicationScheme,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // authorization_challenge_scheme
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationChallengeScheme,

                // we are compatible with Microsoft.AspNetCore.Identity
                // do not use the "Identity.External" scheme as it is only for external identity providers
                Default = IdentityConstants.ApplicationScheme,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // authorization_code_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AuthorizationCodeLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // claims_locales_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsLocalesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // claims_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsParameterSupported,
                Default = false, // TODO: this is still a WIP

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // claims_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // claims_supported_is_strict
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsSupportedIsStrict,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // claim_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimTypesSupported,
                Default = [OpenIdConstants.ClaimTypes.Normal],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // clock_skew
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ClockSkew,
                Default = TimeSpan.FromMinutes(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // continue_authorization_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ContinueAuthorizationLifetime,
                Default = TimeSpan.FromMinutes(15),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // display_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.DisplayValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // grant_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.GrantTypesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_encryption_required
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // id_token_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // id_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.IdTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // id_token_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // op_policy_uri
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderPolicyUri,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // op_tos_uri
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderTermsOfServiceUri,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // prompt_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.PromptValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // redirect_uris
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RedirectUris,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_expiration_policy
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RefreshTokenExpirationPolicy,
                Default = OpenIdConstants.RefreshTokenExpirationPolicy.Absolute,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // refresh_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RefreshTokenLifetime,
                Default = TimeSpan.FromDays(30.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // refresh_token_rotation_enabled
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RefreshTokenRotationEnabled,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // request_object_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // request_object_expected_audience
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestObjectExpectedAudience,
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // request_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // request_uri_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = And,
            };

            // request_uri_require_strict_content_type
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriRequireStrictContentType,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // request_uri_expected_content_type
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestUriExpectedContentType,
                Default = "application/oauth-authz-req+jwt",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // require_pkce
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireCodeChallenge,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Or,
            };

            // require_request_uri_registration
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireRequestUriRegistration,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Or,
            };

            // response_modes_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseModesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // response_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseTypesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
                OnFormat = FormatUniqueCombinations,
            };

            // send_id_claims_in_access_token
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendIdClaimsInAccessToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // service_documentation
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ServiceDocumentation,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace,
            };

            // subject_max_age
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SubjectMaxAge,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Min,
            };

            // subject_type
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SubjectType,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace,
            };

            // subject_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SubjectTypesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // tenant_issuer
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TenantIssuer,

                IsDiscoverable = false,
                OnMerge = Keep,
            };

            // token_endpoint_auth_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // ui_locales_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UiLocalesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect,
            };

            // userinfo_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
            };
        }
    }

    private static string[] FormatUniqueCombinations(
        Setting<IReadOnlyCollection<string>> setting
    ) =>
        setting
            .Value.Order()
            .Aggregate(
                Enumerable.Empty<IReadOnlyCollection<string>>(),
                (acc, value) =>
                    acc.SelectMany(values => new[] { values, values.Append(value).ToArray() })
                        .Append([value]),
                permutations =>
                    permutations
                        .OrderBy(combinations => combinations.Count)
                        .Select(combinations =>
                            string.Join(OpenIdConstants.ParameterSeparatorChar, combinations)
                        )
            )
            .ToArray();
}
