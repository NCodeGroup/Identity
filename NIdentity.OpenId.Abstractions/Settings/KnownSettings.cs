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

namespace NIdentity.OpenId.Settings;

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
    /// Gets the <see cref="SettingDescriptor"/> for the 'acr_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> AcrValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.AcrValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claims_locales_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ClaimsLocalesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ClaimsLocalesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claims_parameter_supported' setting.
    /// </summary>
    public static SettingDescriptor<bool> ClaimsParameterSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ClaimsParameterSupported,
        Discoverable = true,
        OnMerge = And
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claims_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ClaimsSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ClaimsSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claim_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<ClaimType>> ClaimTypesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ClaimTypesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'display_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<DisplayType>> DisplayValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.DisplayValuesSupported,
        Discoverable = true,
        OnMerge = Intersect,
        OnFormat = ConvertToInvariantString
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'grant_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<GrantType>> GrantTypesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.GrantTypesSupported,
        Discoverable = true,
        OnMerge = Intersect,
        OnFormat = ConvertToInvariantString
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> IdTokenEncryptionAlgValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.IdTokenEncryptionAlgValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> IdTokenEncryptionEncValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.IdTokenEncryptionEncValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'id_token_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> IdTokenSigningAlgValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.IdTokenSigningAlgValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'prompt_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<PromptTypes>> PromptValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.PromptValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> RequestObjectEncryptionAlgValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.RequestObjectEncryptionAlgValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> RequestObjectEncryptionEncValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.RequestObjectEncryptionEncValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_object_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> RequestObjectSigningAlgValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.RequestObjectSigningAlgValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_parameter_supported' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequestParameterSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.RequestParameterSupported,
        Discoverable = true,
        OnMerge = And
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'request_uri_parameter_supported' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequestUriParameterSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.RequestUriParameterSupported,
        Discoverable = true,
        OnMerge = And
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'require_request_uri_registration' setting.
    /// </summary>
    public static SettingDescriptor<bool> RequireRequestUriRegistration { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.RequireRequestUriRegistration,
        Discoverable = true,
        OnMerge = Or
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'response_modes_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<ResponseMode>> ResponseModesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ResponseModesSupported,
        Discoverable = true,
        OnMerge = Intersect,
        OnFormat = ConvertToInvariantString
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'response_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<ResponseTypes>> ResponseTypesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ResponseTypesSupported,
        Discoverable = true,
        OnMerge = Intersect,
        OnFormat = ConvertToInvariantString
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'scopes_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ScopesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ScopesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'service_documentation' setting.
    /// </summary>
    public static SettingDescriptor<string> ServiceDocumentation { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.ServiceDocumentation,
        Discoverable = true,
        OnMerge = Replace
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'subject_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> SubjectTypesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.SubjectTypesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'token_endpoint_auth_methods_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> TokenEndpointAuthMethodsSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.TokenEndpointAuthMethodsSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'token_endpoint_auth_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> TokenEndpointAuthSigningAlgValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.TokenEndpointAuthSigningAlgValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'ui_locales_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UiLocalesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.UiLocalesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'userinfo_encryption_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UserInfoEncryptionAlgValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.UserInfoEncryptionAlgValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'userinfo_encryption_enc_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UserInfoEncryptionEncValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.UserInfoEncryptionEncValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'userinfo_signing_alg_values_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> UserInfoSigningAlgValuesSupported { get; } = new()
    {
        SettingName = OpenIdConstants.Parameters.UserInfoSigningAlgValuesSupported,
        Discoverable = true,
        OnMerge = Intersect
    };
}
