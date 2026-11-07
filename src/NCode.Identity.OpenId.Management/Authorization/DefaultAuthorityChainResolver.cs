// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides the default implementation of <see cref="IAuthorityChainResolver"/>.
/// </summary>
internal class DefaultAuthorityChainResolver : IAuthorityChainResolver
{
    /// <inheritdoc />
    // See ADR-1411
    public async ValueTask<
        IReadOnlySet<(string ResourceType, string NormalizedResourceId)>
    > GetAncestorNodesAsync(
        IResourceNode node,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        // A resource's authority chain is fixed: the node itself, its tenant, its team, every team above that team,
        // then the platform root. The platform, team and tenant nodes have globally unique identifiers, so the node
        // itself joins this set only when it is one of them; any other node is matched within its tenant by the
        // caller. A team node adds no tenant node, so a tenant administrator never reaches a team.
        // See ADR-1426.
        HashSet<(string ResourceType, string NormalizedResourceId)> nodes =
        [
            (ResourceNodeTypes.Platform, Normalize(OpenIdConstants.PlatformResourceId)),
        ];

        if (IsGloballyUniqueNodeType(node.ResourceType))
        {
            nodes.Add((node.ResourceType, Normalize(node.ResourceId)));
        }

        string? teamId = null;
        if (string.Equals(node.ResourceType, ResourceNodeTypes.Team, StringComparison.Ordinal))
        {
            teamId = node.ResourceId;
        }
        else if (
            !string.Equals(node.ResourceType, ResourceNodeTypes.Platform, StringComparison.Ordinal)
        )
        {
            nodes.Add((ResourceNodeTypes.Tenant, Normalize(node.TenantId)));

            // An unknown tenant adds no team node.
            var tenantStore = storeManager.GetStore<ITenantStore>();
            var tenant = await tenantStore.GetOrDefaultAsync(node.TenantId, cancellationToken);
            teamId = tenant?.TeamId;
        }

        if (teamId is not null)
        {
            nodes.Add((ResourceNodeTypes.Team, Normalize(teamId)));

            // Authority at a team reaches every team beneath it, so each team above this one joins the chain.
            var teamStore = storeManager.GetStore<ITeamStore>();
            var team = await teamStore.GetOrDefaultAsync(teamId, cancellationToken);
            if (team is not null)
            {
                foreach (var ancestorTeamId in team.AncestorTeamIds)
                {
                    nodes.Add((ResourceNodeTypes.Team, Normalize(ancestorTeamId)));
                }
            }
        }

        return nodes;
    }

    private static bool IsGloballyUniqueNodeType(string resourceType) =>
        string.Equals(resourceType, ResourceNodeTypes.Platform, StringComparison.Ordinal)
        || string.Equals(resourceType, ResourceNodeTypes.Team, StringComparison.Ordinal)
        || string.Equals(resourceType, ResourceNodeTypes.Tenant, StringComparison.Ordinal);

    private static string Normalize(string value) => value.ToLowerInvariant();
}
