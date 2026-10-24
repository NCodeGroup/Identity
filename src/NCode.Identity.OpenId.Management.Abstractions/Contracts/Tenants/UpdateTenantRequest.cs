#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the mutable metadata of an OpenID Tenant that a JSON Patch document may modify. Settings and
/// secrets are managed through their own endpoints.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateTenantRequest
{
    /// <summary>
    /// Gets or sets the issuer identifier for the tenant — its request-independent identity, and therefore the origin of
    /// every absolute URL the server publishes for it. It must be an absolute <c>https</c> URL (<c>http</c> only for a
    /// loopback host) with no user information, query, or fragment, that the configured tenant strategy serves the tenant
    /// beneath. Setting it to <c>null</c> restores the issuer the tenant strategy derives for the tenant from its issuer
    /// template.
    /// </summary>
    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the mutual-TLS endpoint-alias authority for the tenant (RFC 8705 §5) — an <c>https</c> origin such as
    /// <c>https://acme.mtls.example</c> that the discovery document mirrors the tenant's endpoints under, and that the
    /// host-based tenant strategy routes to the tenant. <c>null</c> publishes no aliases.
    /// </summary>
    [JsonPropertyName("mtlsEndpointAliasAuthority")]
    public string? MtlsEndpointAliasAuthority { get; set; }

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is disabled.
    /// </summary>
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }
}
