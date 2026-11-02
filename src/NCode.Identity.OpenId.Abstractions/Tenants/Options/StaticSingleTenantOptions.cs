// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the resolution options for a single tenant that is statically defined.
/// </summary>
[PublicAPI]
public sealed class StaticSingleTenantOptions
{
    /// <summary>
    /// Contains the default value for the tenant's identifier.
    /// </summary>
    public const string DefaultTenantId = "default";

    /// <summary>
    /// Contains the default value for the tenant's display name.
    /// </summary>
    public const string DefaultDisplayName = "Default Tenant";

    /// <summary>
    /// Gets or sets the identifier for the tenant.
    /// </summary>
    public string TenantId { get; set; } = DefaultTenantId;

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    public string DisplayName { get; set; } = DefaultDisplayName;

    /// <summary>
    /// Gets or sets the issuer identifier for the tenant — its request-independent identity, and therefore the origin
    /// of every absolute URL the server publishes (the discovery endpoint URLs, the RFC 7592
    /// <c>registration_client_uri</c>, the device <c>verification_uri</c>, the <c>check_session_iframe</c>). It is
    /// required: the tenant is served only beneath its issuer.
    /// </summary>
    /// <remarks>
    /// Applied when the tenant is first provisioned; it does not update an existing tenant.
    /// </remarks>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the mutual-TLS endpoint-alias authority (RFC 8705 §5) for the tenant — an https origin, for example
    /// <c>https://mtls.example</c>, where the discovery document's <c>mtls_endpoint_aliases</c> are published. Optional.
    /// </summary>
    /// <remarks>
    /// Applied when the tenant is first provisioned; it does not update an existing tenant.
    /// </remarks>
    public string? MtlsEndpointAliasAuthority { get; set; }

    /// <summary>
    /// Gets or sets the relative base path for the tenant.
    /// When specified, this value must include a leading slash.
    /// The default value is undefined (empty).
    /// </summary>
    public PathString TenantPath { get; set; }
}
