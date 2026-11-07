// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides the default implementation of <see cref="IAuthorityChainResolver"/>.
/// </summary>
internal class DefaultAuthorityChainResolver(IAmbientTenantAccessor ambientTenantAccessor)
    : IAuthorityChainResolver
{
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

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
            await AddTeamAndAncestorsAsync(nodes, teamId, storeManager, cancellationToken);
        }

        if (string.Equals(node.ResourceType, ResourceNodeTypes.Client, StringComparison.Ordinal))
        {
            // A machine client sits beneath the node that owns it, so that node's administrators hold authority over it.
            // See ADR-1466.
            string? owningTeamId = null;
            using (AmbientTenantAccessor.BeginScope(node.TenantId))
            {
                var clientStore = storeManager.GetStore<IClientStore>();
                var client = await clientStore.GetOrDefaultAsync(
                    node.ResourceId,
                    cancellationToken
                );
                if (
                    client is not null
                    && string.Equals(
                        client.TenantId,
                        node.TenantId,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && string.Equals(
                        client.OwningNodeType,
                        ResourceNodeTypes.Team,
                        StringComparison.Ordinal
                    )
                )
                {
                    owningTeamId = client.OwningNodeId;
                }
            }

            if (owningTeamId is not null)
            {
                await AddTeamAndAncestorsAsync(
                    nodes,
                    owningTeamId,
                    storeManager,
                    cancellationToken
                );
            }
        }

        return nodes;
    }

    private static async ValueTask AddTeamAndAncestorsAsync(
        HashSet<(string ResourceType, string NormalizedResourceId)> nodes,
        string teamId,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
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

    private static bool IsGloballyUniqueNodeType(string resourceType) =>
        string.Equals(resourceType, ResourceNodeTypes.Platform, StringComparison.Ordinal)
        || string.Equals(resourceType, ResourceNodeTypes.Team, StringComparison.Ordinal)
        || string.Equals(resourceType, ResourceNodeTypes.Tenant, StringComparison.Ordinal);

    private static string Normalize(string value) => value.ToLowerInvariant();
}
