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

using System.ComponentModel;
using System.Globalization;
using NCode.Jose;

namespace NCode.Identity.OpenId.Settings;

// References:
// https://openid.net/specs/openid-connect-discovery-1_0.html

// TODO
// op_policy_uri (OPTIONAL)
// op_tos_uri (OPTIONAL)

/// <summary>
/// Contains constants for known <see cref="SettingDescriptor"/> instances.
/// </summary>
public static class KnownSettings
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

    /// <summary>
    /// Provides a format function that converts the items in a collection to their invariant string values
    /// using <see cref="TypeConverter"/>.
    /// </summary>
    public static List<string> ConvertToInvariantString<TItem>(IEnumerable<TItem> collection)
        where TItem : IConvertible
    {
        var converter = TypeDescriptor.GetConverter(typeof(TItem));
        return collection.Select(item =>
            converter.ConvertToInvariantString(item) ?? item.ToString(CultureInfo.InvariantCulture)
        ).ToList();
    }

    //

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'access_token_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> AccessTokenEncryptionAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.AccessTokenEncryptionAlgValuesSupported,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'access_token_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> AccessTokenEncryptionEncValuesSupported { get; } = new()
    {
        Name = SettingNames.AccessTokenEncryptionEncValuesSupported,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'access_token_encryption_required' setting.
    /// </summary>
    public static SettingDescriptor<bool> AccessTokenEncryptionRequired { get; } = new()
    {
        Name = SettingNames.AccessTokenEncryptionRequired,
        Default = false,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'access_token_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> AccessTokenEncryptionZipValuesSupported { get; } = new()
    {
        Name = SettingNames.AccessTokenEncryptionZipValuesSupported,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'access_token_lifetime' setting.
    /// </summary>
    public static SettingDescriptor<TimeSpan> AccessTokenLifetime { get; } = new()
    {
        Name = SettingNames.AccessTokenLifetime,
        Default = TimeSpan.FromMinutes(5.0),

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'access_token_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> AccessTokenSigningAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.AccessTokenSigningAlgValuesSupported,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'access_token_type' setting.
    /// </summary>
    public static SettingDescriptor<string> AccessTokenType { get; } = new()
    {
        Name = SettingNames.AccessTokenType,
        Default = JoseTokenTypes.Jwt,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'acr_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> AcrValuesSupported { get; } = new()
    {
        Name = SettingNames.AcrValuesSupported,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'allow_loopback_redirect' setting.
    /// </summary>
    public static SettingDescriptor<bool> AllowLoopbackRedirect { get; } = new()
    {
        Name = SettingNames.AllowLoopbackRedirect,
        Default = true,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'allow_plain_code_challenge_method' setting.
    /// </summary>
    public static SettingDescriptor<bool> AllowPlainCodeChallengeMethod { get; } = new()
    {
        Name = SettingNames.AllowPlainCodeChallengeMethod,
        Default = true,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'allow_unsafe_token_response' setting.
    /// </summary>
    public static SettingDescriptor<bool> AllowUnsafeTokenResponse { get; } = new()
    {
        Name = SettingNames.AllowUnsafeTokenResponse,
        Default = true,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'authorization_code_lifetime' setting.
    /// </summary>
    public static SettingDescriptor<TimeSpan> AuthorizationCodeLifetime { get; } = new()
    {
        Name = SettingNames.AuthorizationCodeLifetime,
        Default = TimeSpan.FromMinutes(5.0),

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'authorization_sign_in_scheme' setting.
    /// </summary>
    public static SettingDescriptor<string> AuthorizationSignInScheme { get; } = new()
    {
        Name = SettingNames.AuthorizationSignInScheme,
        Default = string.Empty,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claims_locales_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ClaimsLocalesSupported { get; } = new()
    {
        Name = SettingNames.ClaimsLocalesSupported,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claims_parameter_supported' setting.
    /// </summary>
    public static SettingDescriptor<bool> ClaimsParameterSupported { get; } = new()
    {
        Name = SettingNames.ClaimsParameterSupported,
        Default = true,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claims_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ClaimsSupported { get; } = new()
    {
        Name = SettingNames.ClaimsSupported,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claim_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ClaimTypesSupported { get; } = new()
    {
        Name = SettingNames.ClaimTypesSupported,
        Default = new[] { OpenIdConstants.ClaimTypes.Normal },

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'clock_skew' setting.
    /// </summary>
    public static SettingDescriptor<TimeSpan> ClockSkew { get; } = new()
    {
        Name = SettingNames.ClockSkew,
        Default = TimeSpan.FromMinutes(5),

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'continue_authorization_lifetime' setting.
    /// </summary>
    public static SettingDescriptor<TimeSpan> ContinueAuthorizationLifetime { get; } = new()
    {
        Name = SettingNames.ContinueAuthorizationLifetime,
        Default = TimeSpan.FromMinutes(15),

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'display_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<DisplayType>> DisplayValuesSupported { get; } = new()
    {
        Name = SettingNames.DisplayValuesSupported,
        // TODO: Default

        Discoverable = true,
        OnMerge = Replace,
        OnFormat = ConvertToInvariantString
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'grant_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> GrantTypesSupported { get; } = new()
    {
        Name = SettingNames.GrantTypesSupported,
        Default = new[] { OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit },

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> IdTokenEncryptionAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.IdTokenEncryptionAlgValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> IdTokenEncryptionEncValuesSupported { get; } = new()
    {
        Name = SettingNames.IdTokenEncryptionEncValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_encryption_required' setting.
    /// </summary>
    public static SettingDescriptor<bool> IdTokenEncryptionRequired { get; } = new()
    {
        Name = SettingNames.IdTokenEncryptionRequired,
        Default = false,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> IdTokenEncryptionZipValuesSupported { get; } = new()
    {
        Name = SettingNames.IdTokenEncryptionZipValuesSupported,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_lifetime' setting.
    /// </summary>
    public static SettingDescriptor<TimeSpan> IdTokenLifetime { get; } = new()
    {
        Name = SettingNames.IdTokenLifetime,
        Default = TimeSpan.FromMinutes(5.0),

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> IdTokenSigningAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.IdTokenSigningAlgValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'prompt_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<PromptTypes>> PromptValuesSupported { get; } = new()
    {
        Name = SettingNames.PromptValuesSupported,
        // TODO: Default

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> RequestObjectEncryptionAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.RequestObjectEncryptionAlgValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> RequestObjectEncryptionEncValuesSupported { get; } = new()
    {
        Name = SettingNames.RequestObjectEncryptionEncValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> RequestObjectEncryptionZipValuesSupported { get; } = new()
    {
        Name = SettingNames.RequestObjectEncryptionZipValuesSupported,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> RequestObjectSigningAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.RequestObjectSigningAlgValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_expected_audience' setting.
    /// </summary>
    public static SettingDescriptor<string> RequestObjectExpectedAudience { get; } = new()
    {
        Name = SettingNames.RequestObjectExpectedAudience,
        Default = string.Empty,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_parameter_supported' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequestParameterSupported { get; } = new()
    {
        Name = SettingNames.RequestParameterSupported,
        Default = true,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_uri_parameter_supported' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequestUriParameterSupported { get; } = new()
    {
        Name = SettingNames.RequestUriParameterSupported,
        Default = true,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_uri_require_strict_content_type' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequestUriRequireStrictContentType { get; } = new()
    {
        Name = SettingNames.RequestUriRequireStrictContentType,
        Default = false,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_uri_expected_content_type' setting.
    /// </summary>
    public static SettingDescriptor<string> RequestUriExpectedContentType { get; } = new()
    {
        Name = SettingNames.RequestUriExpectedContentType,
        Default = "application/oauth-authz-req+jwt",

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'require_pkce' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequireCodeChallenge { get; } = new()
    {
        Name = SettingNames.RequireCodeChallenge,
        Default = false,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'require_request_uri_registration' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequireRequestUriRegistration { get; } = new()
    {
        Name = SettingNames.RequireRequestUriRegistration,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'response_modes_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<ResponseMode>> ResponseModesSupported { get; } = new()
    {
        Name = SettingNames.ResponseModesSupported,
        Default = new[]
        {
            ResponseMode.Query,
            ResponseMode.Fragment,
            ResponseMode.FormPost
        },

        Discoverable = true,
        OnMerge = Replace,
        OnFormat = ConvertToInvariantString
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'response_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<ResponseTypes>> ResponseTypesSupported { get; } = new()
    {
        Name = SettingNames.ResponseTypesSupported,
        Default = new[]
        {
            ResponseTypes.None,
            ResponseTypes.Code,
            ResponseTypes.IdToken,
            ResponseTypes.Code | ResponseTypes.IdToken,
            ResponseTypes.Token | ResponseTypes.IdToken
        },

        Discoverable = true,
        OnMerge = Replace,
        OnFormat = ConvertToInvariantString
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'scopes_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ScopesSupported { get; } = new()
    {
        Name = SettingNames.ScopesSupported,
        Default = new[]
        {
            OpenIdConstants.ScopeTypes.OpenId,
            OpenIdConstants.ScopeTypes.Profile,
            OpenIdConstants.ScopeTypes.Email,
            OpenIdConstants.ScopeTypes.Address,
            OpenIdConstants.ScopeTypes.Phone,
            OpenIdConstants.ScopeTypes.OfflineAccess
        },

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'service_documentation' setting.
    /// </summary>
    public static SettingDescriptor<string> ServiceDocumentation { get; } = new()
    {
        Name = SettingNames.ServiceDocumentation,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'subject_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> SubjectTypesSupported { get; } = new()
    {
        Name = SettingNames.SubjectTypesSupported,
        // TODO: Default

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'token_endpoint_auth_methods_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> TokenEndpointAuthMethodsSupported { get; } = new()
    {
        Name = SettingNames.TokenEndpointAuthMethodsSupported,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'token_endpoint_auth_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> TokenEndpointAuthSigningAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.TokenEndpointAuthSigningAlgValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'ui_locales_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UiLocalesSupported { get; } = new()
    {
        Name = SettingNames.UiLocalesSupported,

        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'userinfo_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UserInfoEncryptionAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.UserInfoEncryptionAlgValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'userinfo_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UserInfoEncryptionEncValuesSupported { get; } = new()
    {
        Name = SettingNames.UserInfoEncryptionEncValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'userinfo_encryption_zip_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UserInfoEncryptionZipValuesSupported { get; } = new()
    {
        Name = SettingNames.UserInfoEncryptionZipValuesSupported,

        Discoverable = IsNonStdDiscoverable,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'userinfo_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UserInfoSigningAlgValuesSupported { get; } = new()
    {
        Name = SettingNames.UserInfoSigningAlgValuesSupported,

        Discoverable = true,
        OnMerge = Intersect
    };
}
