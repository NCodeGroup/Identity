// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the request body to create a new OpenID Tenant.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateTenantRequest
{
    /// <summary>
    /// Gets the identifier for the tenant: a name its creator chooses, which cannot be changed once the tenant exists.
    /// It is 1 to 63 lowercase letters, digits, or hyphens that starts and ends with a letter or digit, so it is equally
    /// valid as a URL path segment and as a hostname label. It addresses the tenant in the management API and, when
    /// tenants are served by path, is the final segment of the tenant's issuer.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the optional issuer identifier for the tenant — its request-independent identity, and therefore the origin
    /// of every absolute URL the server publishes for it. It must be an absolute <c>https</c> URL (<c>http</c> only for
    /// a loopback host) with no user information, query, or fragment, that the configured tenant strategy serves the
    /// tenant beneath. When <c>null</c>, the tenant is given the issuer its tenant strategy derives for it from its issuer
    /// template; a tenant the strategy serves cannot be created without one.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Gets the mutual-TLS endpoint-alias authority for the tenant (RFC 8705 §5) — an <c>https</c> origin such as
    /// <c>https://acme.mtls.example</c> that the discovery document mirrors the tenant's endpoints under, and that the
    /// host-based tenant strategy routes to the tenant. <c>null</c> publishes no aliases.
    /// </summary>
    public string? MtlsEndpointAliasAuthority { get; init; }

    /// <summary>
    /// Gets the display name for the tenant.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tenant is created in a disabled state.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the initial JSON settings for the tenant.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
