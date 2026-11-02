// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
