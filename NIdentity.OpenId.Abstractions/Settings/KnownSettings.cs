﻿#region Copyright Preamble

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

namespace NIdentity.OpenId.Settings;

// TODO: discovery configuration
// https://openid.net/specs/openid-connect-discovery-1_0.html

// scopes_supported (RECOMMENDED)
// response_types_supported (REQUIRED)
// response_modes_supported (OPTIONAL)
// grant_types_supported (OPTIONAL)
// acr_values_supported (OPTIONAL)
// subject_types_supported (REQUIRED)
// id_token_signing_alg_values_supported (REQUIRED)
// id_token_encryption_alg_values_supported (OPTIONAL)
// id_token_encryption_enc_values_supported (OPTIONAL)
// userinfo_signing_alg_values_supported (OPTIONAL)
// userinfo_encryption_alg_values_supported (OPTIONAL)
// userinfo_encryption_enc_values_supported (OPTIONAL)
// request_object_signing_alg_values_supported (OPTIONAL)
// request_object_encryption_alg_values_supported (OPTIONAL)
// request_object_encryption_enc_values_supported (OPTIONAL)
// token_endpoint_auth_methods_supported (OPTIONAL)
// token_endpoint_auth_signing_alg_values_supported (OPTIONAL)
// display_values_supported (OPTIONAL)
// claim_types_supported (OPTIONAL)
// claims_supported (RECOMMENDED)
// claims_locales_supported (OPTIONAL)
// ui_locales_supported (OPTIONAL)
// claims_parameter_supported (OPTIONAL)
// request_parameter_supported (OPTIONAL)
// request_uri_parameter_supported (OPTIONAL)
// require_request_uri_registration (OPTIONAL)

// service_documentation (OPTIONAL)
// op_policy_uri (OPTIONAL)
// op_tos_uri (OPTIONAL)

// https://openid.net/specs/openid-connect-prompt-create-1_0-05.html
// prompt_values_supported (OPTIONAL)

/// <summary>
/// Contains constants for known <see cref="SettingDescriptor"/> instances.
/// </summary>
public static class KnownSettings
{
    private static bool And(bool current, bool other) => current && other;

    private static List<TValue> Intersect<TValue>(
        IEnumerable<TValue> current,
        IEnumerable<TValue> other
    ) => current.Intersect(other).ToList();

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'scopes_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<string>> ScopesSupported { get; } = new()
    {
        SettingName = "scopes_supported",
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'response_types_supported' setting.
    /// </summary>
    public static SettingDescriptor<IReadOnlyCollection<ResponseTypes>> ResponseTypesSupported { get; } = new()
    {
        SettingName = "response_types_supported",
        Discoverable = true,
        OnMerge = Intersect
    };

    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> for the 'claims_parameter_supported' setting.
    /// </summary>
    public static SettingDescriptor<bool> ClaimsParameterSupported { get; } = new()
    {
        SettingName = "claims_parameter_supported",
        Discoverable = true,
        OnMerge = And
    };
}