// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides the chain of nodes whose role assignments hold authority over a node.
/// </summary>
internal interface IAuthorityChainResolver
{
    /// <summary>
    /// Gets the globally unique nodes whose role assignments hold authority over a node: the platform, the node itself
    /// when it is a platform, team or tenant node, its tenant, that tenant's team and every team above it, and, for a
    /// machine client, the team that owns it and every team above that team.
    /// </summary>
    /// <param name="node">The node to resolve the chain for.</param>
    /// <param name="storeManager">The store manager that reads the tenant, client and team rows.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The resource type and normalized resource identifier of each node in the chain.</returns>
    ValueTask<
        IReadOnlySet<(string ResourceType, string NormalizedResourceId)>
    > GetAncestorNodesAsync(
        IResourceNode node,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
