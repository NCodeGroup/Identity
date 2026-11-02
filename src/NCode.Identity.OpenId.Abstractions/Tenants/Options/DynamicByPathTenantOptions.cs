// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the resolution options for dynamic tenant resolution based on the path.
/// </summary>
[PublicAPI]
public sealed class DynamicByPathTenantOptions
{
    /// <summary>
    /// Contains the name of the route parameter that will be used to determine the tenant identifier.
    /// This value must be contained within the <see cref="TenantPath"/> route pattern.
    /// The default value is 'tenantId'.
    /// </summary>
    public string TenantIdRouteParameterName { get; set; } = "tenantId";

    /// <summary>
    /// Gets or sets the relative base path for the tenant.
    /// This value must be set to a route pattern that includes the tenant identifier as a route parameter with a leading slash.
    /// The default value is '/{tenantId}'.
    /// </summary>
    public PathString TenantPath { get; set; } = "/{tenantId}";

    /// <summary>
    /// Gets or sets the template a tenant's issuer is derived from when the tenant is written without one — an absolute
    /// URL whose path ends with <see cref="TenantPath"/>, carrying <see cref="TenantResolutionOptions.TenantIdPlaceholder"/>
    /// where the route carries the tenant identifier, for example <c>https://op.example/{tenantId}</c>. The derived
    /// issuer is stored with the tenant, so changing the template never moves an existing tenant. When unset, every
    /// tenant must be given an issuer explicitly.
    /// </summary>
    public string? IssuerTemplate { get; set; }
}
