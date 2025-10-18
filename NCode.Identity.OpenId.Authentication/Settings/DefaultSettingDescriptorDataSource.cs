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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="SettingDescriptor"/> instances supported by this library.
/// </summary>
[PublicAPI]
public class DefaultSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IServiceProvider serviceProvider
) : ICollectionDataSource<SettingDescriptor>
{
    private const bool IsStdDiscoverable = true;
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

    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    private List<string>? AuthMethodsOrNull { get; set; }
    private List<string> AuthMethods => AuthMethodsOrNull ??= GetAuthMethods();

    private List<string> GetAuthMethods()
    {
        return ServiceProvider
            .GetServices<IClientAuthenticationHandler>()
            .Select(handler => handler.AuthenticationMethod)
            .ToList();
    }

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
                Name = OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_encryption_required
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // access_token_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AccessTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // access_token_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AccessTokenSigningAlgValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // access_token_type
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AccessTokenType,
                Default = JoseTokenTypes.Jwt,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // acr_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AcrValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // allow_loopback_redirect
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowLoopbackRedirect,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // allow_plain_code_challenge_method
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowPlainCodeChallengeMethod,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // allow_unsafe_token_response
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.AllowUnsafeTokenResponse,
                Default = true,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // allowed_identity_providers
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.AllowedIdentityProviders,
                Default = [],

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // authorization_authenticate_scheme
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationAuthenticateScheme,

                // we are compatible with Microsoft.AspNetCore.Identity
                // do not use the "Identity.External" scheme as it is only for external identity providers
                Default = IdentityConstants.ApplicationScheme,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // authorization_challenge_scheme
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.AuthorizationChallengeScheme,

                // we are compatible with Microsoft.AspNetCore.Identity
                // do not use the "Identity.External" scheme as it is only for external identity providers
                Default = IdentityConstants.ApplicationScheme,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // authorization_code_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.AuthorizationCodeLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // claims_locales_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsLocalesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // claims_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsParameterSupported,
                Default = false, // TODO: this is still a WIP

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // claims_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimsSupported,
                Default =
                [
                    ..OpenIdConstants.ProtocolClaims,
                    ..OpenIdConstants.ClaimsByScope.Profile,
                    ..OpenIdConstants.ClaimsByScope.Email,
                    ..OpenIdConstants.ClaimsByScope.Address,
                    ..OpenIdConstants.ClaimsByScope.Phone,
                ],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // claims_supported_is_strict
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.ClaimsSupportedIsStrict,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // claim_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ClaimTypesSupported,
                Default = [OpenIdConstants.ClaimTypes.Normal],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // clock_skew
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ClockSkew,
                Default = TimeSpan.FromMinutes(5),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // continue_authorization_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.ContinueAuthorizationLifetime,
                Default = TimeSpan.FromMinutes(15),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // display_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.DisplayValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // grant_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.GrantTypesSupported,
                Default =
                [
                    OpenIdConstants.GrantTypes.AuthorizationCode,
                    OpenIdConstants.GrantTypes.Implicit
                ],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // id_token_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // id_token_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // id_token_encryption_required
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionRequired,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // id_token_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // id_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.IdTokenLifetime,
                Default = TimeSpan.FromMinutes(5.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // id_token_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.IdTokenSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // op_policy_uri
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderPolicyUri,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // op_tos_uri
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.OpenIdProviderTermsOfServiceUri,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // prompt_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.PromptValuesSupported,
                Default =
                [
                    OpenIdConstants.PromptTypes.None,
                    OpenIdConstants.PromptTypes.Login,
                    OpenIdConstants.PromptTypes.Consent,
                    OpenIdConstants.PromptTypes.SelectAccount,
                    OpenIdConstants.PromptTypes.CreateAccount
                ],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // redirect_uris
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RedirectUris,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // refresh_token_expiration_policy
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RefreshTokenExpirationPolicy,
                Default = OpenIdConstants.RefreshTokenExpirationPolicy.Absolute,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // refresh_token_lifetime
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.RefreshTokenLifetime,
                Default = TimeSpan.FromDays(30.0),

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // refresh_token_rotation_enabled
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RefreshTokenRotationEnabled,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // request_object_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // request_object_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // request_object_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // request_object_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // request_object_expected_audience
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestObjectExpectedAudience,
                Default = string.Empty,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // request_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // request_uri_parameter_supported
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriParameterSupported,
                Default = true,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // request_uri_require_strict_content_type
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequestUriRequireStrictContentType,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // request_uri_expected_content_type
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.RequestUriExpectedContentType,
                Default = "application/oauth-authz-req+jwt",

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // require_pkce
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireCodeChallenge,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // require_request_uri_registration
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.RequireRequestUriRegistration,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // response_modes_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseModesSupported,
                Default =
                [
                    OpenIdConstants.ResponseModes.Query,
                    OpenIdConstants.ResponseModes.Fragment,
                    OpenIdConstants.ResponseModes.FormPost
                ],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // response_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ResponseTypesSupported,
                Default =
                [
                    OpenIdConstants.ResponseTypes.Code,
                    OpenIdConstants.ResponseTypes.IdToken,
                    OpenIdConstants.ResponseTypes.Token
                ],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect,
                OnFormat = FormatUniqueCombinations
            };

            // scopes_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.ScopesSupported,
                Default =
                [
                    OpenIdConstants.ScopeTypes.OpenId,
                    OpenIdConstants.ScopeTypes.Profile,
                    OpenIdConstants.ScopeTypes.Email,
                    OpenIdConstants.ScopeTypes.Address,
                    OpenIdConstants.ScopeTypes.Phone,
                    OpenIdConstants.ScopeTypes.OfflineAccess
                ],

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // send_id_claims_in_access_token
            yield return new SettingDescriptor<bool>
            {
                Name = OpenIdSettingNames.SendIdClaimsInAccessToken,
                Default = false,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // service_documentation
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.ServiceDocumentation,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // subject_max_age
            yield return new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SubjectMaxAge,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // subject_type
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.SubjectType,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Replace
            };

            // subject_types_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.SubjectTypesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Replace
            };

            // tenant_issuer
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TenantIssuer,

                IsDiscoverable = false,
                OnMerge = Replace
            };

            // token_endpoint_auth_methods_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthMethodsSupported,
                Default = AuthMethods,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // token_endpoint_auth_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // ui_locales_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UiLocalesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // userinfo_encryption_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // userinfo_encryption_enc_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionEncValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
                OnMerge = Intersect
            };

            // userinfo_encryption_zip_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoEncryptionZipValuesSupported,

                IsDiscoverable = IsNonStdDiscoverable,
                OnMerge = Intersect
            };

            // userinfo_signing_alg_values_supported
            yield return new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.UserInfoSigningAlgValuesSupported,

                IsDiscoverable = IsStdDiscoverable,
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
