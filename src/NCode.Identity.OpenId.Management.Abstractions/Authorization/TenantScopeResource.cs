// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// A minimal <see cref="ISupportTenantId"/> authorization resource used to authorize a tenant-scoped operation (for
/// example, creating or listing a tenant-bound family) against the ambient tenant. Unlike a resource node it
/// deliberately carries no resource type or identifier, so only the tenant- and global-admin handlers evaluate it and
/// the ownership handler does not — there is no single resource instance (and no ownership) to consider.
/// </summary>
[PublicAPI]
public sealed class TenantScopeResource : ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Creates a <see cref="TenantScopeResource"/> for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to authorize against.</param>
    /// <returns>The tenant-scope resource.</returns>
    public static TenantScopeResource For(string tenantId) => new() { TenantId = tenantId };
}
