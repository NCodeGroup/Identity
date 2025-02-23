﻿#region Copyright Preamble

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

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Jose;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="SettingDescriptor"/> instances supported by this library.
/// </summary>
[PublicAPI]
public class DefaultSettingDescriptorDataSource(
    INullChangeToken nullChangeToken
) : ICollectionDataSource<SettingDescriptor>
{
    private const bool IsNonStdDiscoverable = false;

    /// <summary>
    /// Provides a merge function that returns the logical <c>AND</c> of the two values.
    /// </summary>
    public static bool And(bool current, bool other) => current && other;

    /// <summary>
    /// Provides a merge function that returns the logical <c>OR</c> of the two values.
    /// </summary>
    public static bool Or(bool current, bool other) => current || other;

    /// <summary>
    /// Provides a merge function that always returns the other value.
    /// </summary>
    public static TValue Replace<TValue>(TValue _, TValue other) => other;

    /// <summary>
    /// Provides a merge function that returns the intersection of the two collections.
    /// </summary>
    public static List<TItem> Intersect<TItem>(
        IEnumerable<TItem> current,
        IEnumerable<TItem> other
    ) => current.Intersect(other).ToList();

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // access_token_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.AccessTokenEncryptionAlgValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.AccessTokenEncryptionEncValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_encryption_required
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.AccessTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // access_token_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.AccessTokenEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = SettingNames.AccessTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // access_token_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.AccessTokenSigningAlgValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_type
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.AccessTokenType,
                Default = JoseTokenTypes.Jwt,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // acr_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.AcrValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // allow_loopback_redirect
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.AllowLoopbackRedirect,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // allow_plain_code_challenge_method
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.AllowPlainCodeChallengeMethod,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // allow_unsafe_token_response
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.AllowUnsafeTokenResponse,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // authorization_authenticate_scheme
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.AuthorizationAuthenticateScheme,
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // authorization_challenge_scheme
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.AuthorizationChallengeScheme,
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // authorization_code_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = SettingNames.AuthorizationCodeLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // claims_locales_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.ClaimsLocalesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // claims_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.ClaimsParameterSupported,
                Default = true,

                IsDiscoverable = true,
                OnMerge = Replace
            };

            // claims_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.ClaimsSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // claim_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.ClaimTypesSupported,
                Default = [OpenIdConstants.ClaimTypes.Normal],

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // clock_skew
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = SettingNames.ClockSkew,
                Default = TimeSpan.FromMinutes(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // continue_authorization_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = SettingNames.ContinueAuthorizationLifetime,
                Default = TimeSpan.FromMinutes(15),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // display_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.DisplayValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // grant_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.GrantTypesSupported,
                Default =
                [
                    OpenIdConstants.GrantTypes.AuthorizationCode,
                    OpenIdConstants.GrantTypes.Implicit
                ],

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // id_token_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.IdTokenEncryptionAlgValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // id_token_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.IdTokenEncryptionEncValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // id_token_encryption_required
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.IdTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // id_token_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.IdTokenEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // id_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = SettingNames.IdTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // id_token_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.IdTokenSigningAlgValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // prompt_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.PromptValuesSupported,
                Default =
                [
                    OpenIdConstants.PromptTypes.None,
                    OpenIdConstants.PromptTypes.Login,
                    OpenIdConstants.PromptTypes.Consent,
                    OpenIdConstants.PromptTypes.SelectAccount,
                    OpenIdConstants.PromptTypes.CreateAccount
                ],

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // refresh_token_expiration_policy
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.RefreshTokenExpirationPolicy,
                Default = OpenIdConstants.RefreshTokenExpirationPolicy.Absolute,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // refresh_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = SettingNames.RefreshTokenLifetime,
                Default = TimeSpan.FromDays(30.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // refresh_token_rotation_enabled
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.RefreshTokenRotationEnabled,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // request_object_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.RequestObjectEncryptionAlgValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // request_object_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.RequestObjectEncryptionEncValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // request_object_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.RequestObjectEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // request_object_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.RequestObjectSigningAlgValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // request_object_expected_audience
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.RequestObjectExpectedAudience,
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // request_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.RequestParameterSupported,
                Default = true,

                IsDiscoverable = true,
                OnMerge = Replace
            };

            // request_uri_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.RequestUriParameterSupported,
                Default = true,

                IsDiscoverable = true,
                OnMerge = Replace
            };

            // request_uri_require_strict_content_type
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.RequestUriRequireStrictContentType,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // request_uri_expected_content_type
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.RequestUriExpectedContentType,
                Default = "application/oauth-authz-req+jwt",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // require_pkce
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.RequireCodeChallenge,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // require_request_uri_registration
            yield return new SettingDescriptor<bool>
            {
                Name = SettingNames.RequireRequestUriRegistration,

                IsDiscoverable = true,
                OnMerge = Replace
            };

            // response_modes_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.ResponseModesSupported,
                Default =
                [
                    OpenIdConstants.ResponseModes.Query,
                    OpenIdConstants.ResponseModes.Fragment,
                    OpenIdConstants.ResponseModes.FormPost
                ],

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // response_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.ResponseTypesSupported,
                Default =
                [
                    OpenIdConstants.ResponseTypes.Code,
                    OpenIdConstants.ResponseTypes.IdToken,
                    OpenIdConstants.ResponseTypes.Token
                ],

                IsDiscoverable = true,
                OnMerge = Intersect,
                OnFormat = FormatUniqueCombinations
            };

            // scopes_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.ScopesSupported,
                Default =
                [
                    OpenIdConstants.ScopeTypes.OpenId,
                    OpenIdConstants.ScopeTypes.Profile,
                    OpenIdConstants.ScopeTypes.Email,
                    OpenIdConstants.ScopeTypes.Address,
                    OpenIdConstants.ScopeTypes.Phone,
                    OpenIdConstants.ScopeTypes.OfflineAccess
                ],

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // service_documentation
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.ServiceDocumentation,

                IsDiscoverable = true,
                OnMerge = Replace
            };

            // subject_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.SubjectTypesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // tenant_issuer
            yield return new SettingDescriptor<string>
            {
                Name = SettingNames.TenantIssuer,

                IsDiscoverable = false,
                OnMerge = Replace
            };

            // token_endpoint_auth_methods_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.TokenEndpointAuthMethodsSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // token_endpoint_auth_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.TokenEndpointAuthSigningAlgValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // ui_locales_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.UiLocalesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // userinfo_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.UserInfoEncryptionAlgValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // userinfo_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.UserInfoEncryptionEncValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };

            // userinfo_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.UserInfoEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // userinfo_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = SettingNames.UserInfoSigningAlgValuesSupported,

                IsDiscoverable = true,
                OnMerge = Intersect
            };
        }
    }

    private static string[] FormatUniqueCombinations(Setting<IReadOnlyCollection<string>> setting) =>
        setting.Value
            .Order()
            .Aggregate(
                Enumerable.Empty<IReadOnlyCollection<string>>(),
                (acc, value) => acc
                    .SelectMany(values => new[] { values, values.Append(value).ToArray() })
                    .Append([value]),
                permutations => permutations
                    .OrderBy(combinations => combinations.Count)
                    .Select(combinations => string.Join(OpenIdConstants.ParameterSeparatorChar, combinations)))
            .ToArray();
}
