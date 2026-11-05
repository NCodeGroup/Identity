// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// A minimal <see cref="ISupportTenantId"/> authorization resource used to authorize a tenant-scoped operation (for
/// example, creating or listing a tenant-bound family) against the ambient tenant. Unlike a resource node it
/// deliberately carries no resource type or identifier, so it is judged at its tenant's node: the
/// global-admin handler evaluates it, and the ownership handler judges it through the tenant node and its ancestors
/// (the tenant's team and the platform). <c>Owner</c>, <c>TeamAdmin</c>, <c>TenantAdmin</c> and <c>GlobalAdmin</c>
/// authority at those nodes reaches it, and creation is held by <c>TeamAdmin</c>, <c>TenantAdmin</c> and
/// <c>GlobalAdmin</c>.
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
