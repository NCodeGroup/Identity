// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// A minimal <see cref="IResourceNode"/> an endpoint passes to <c>IAuthorizationService.AuthorizeAsync</c> so that the
/// ownership authorization handler can evaluate resource-scoped assignments for the resource being acted on.
/// </summary>
[PublicAPI]
public sealed class ResourceNode : IResourceNode
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc />
    public required string ResourceType { get; init; }

    /// <inheritdoc />
    public required string ResourceId { get; init; }

    /// <summary>
    /// Creates a <see cref="ResourceNode"/> for the specified type and identifier within a tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the owning tenant.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <returns>The resource node.</returns>
    public static ResourceNode For(string tenantId, string resourceType, string resourceId) =>
        new()
        {
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
        };
}
