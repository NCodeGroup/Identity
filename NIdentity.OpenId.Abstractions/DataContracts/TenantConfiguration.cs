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

using System.Text.Json.Serialization;

namespace NIdentity.OpenId.DataContracts;

/// <summary>
/// Contains the configurable settings for an <c>OAuth</c> or <c>OpenID Connect</c> tenant.
/// </summary>
public class TenantConfiguration
{
    /// <summary>
    /// Gets or sets the identifier for the tenant.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the domain name for the tenant.
    /// This value is optional and can be used to find tenants by domain name.
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issuer identifier for the tenant.
    /// If specified, the host address must be in unicode and not punycode.
    /// If unspecified, the default value will be determined by the host address and tenant path.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the amount of time to allow for clock skew when validating <see cref="DateTime"/> claims.
    /// The default is <c>300</c> seconds (5 minutes).
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or set the <see cref="AuthorizationConfiguration"/> for the authorization handler.
    /// </summary>
    public AuthorizationConfiguration Authorization { get; set; } = new();

    /// <summary>
    /// Gets the <see cref="IDictionary{TKey, TValue}"/> for extension members.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, object?> ExtensionData { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);

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
}
