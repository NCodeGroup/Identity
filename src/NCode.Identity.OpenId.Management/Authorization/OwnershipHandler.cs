// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// An <see cref="AuthorizationHandler{TRequirement,TResource}"/> that grants a principal authority over a tenant-plane
/// resource. Any <see cref="ISupportTenantId"/> resource is judged at its <see cref="IResourceNode"/>; a persisted
/// entity or a <see cref="TenantScopeResource"/> is judged at its tenant's node, and any other node type inherits the
/// tenant's and the team's assignments. A principal holds authority when it has a role assignment at that node or an
/// ancestor: the chain is the node, its tenant, its team, every team above that team, then the platform root. The platform, team and tenant nodes have globally unique identifiers and match on
/// type and identifier; any other node's identifier is unique only within its tenant, so an assignment at it counts
/// only when it belongs to the node's tenant. This realizes ownership: an <c>Owner</c> assignment at an instance lets the principal
/// manage that instance (and everything beneath it) without any tenant-wide role. Ownership grants every operation
/// except <see cref="Operations.Create"/>; creating a new resource is authority at the parent node, held by a
/// <c>TeamAdmin</c>, <c>TenantAdmin</c> or <c>GlobalAdmin</c> assignment at the node or an ancestor.
/// </summary>
internal class OwnershipHandler(
    IStoreManagerFactory storeManagerFactory,
    IPrincipalResolver principalResolver,
    IHttpContextAccessor httpContextAccessor,
    IAmbientTenantAccessor ambientTenantAccessor
) : AuthorizationHandler<IAuthorizationRequirement, ISupportTenantId>
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;

    // ASP.NET Core authorization handlers are invoked by the framework without the request's OpenIdContext in hand, so
    // this is the one blessed place that reads the ambient context from the request rather than receiving it threaded.
    private IHttpContextAccessor HttpContextAccessor { get; } = httpContextAccessor;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement,
        ISupportTenantId resource
    )
    {
        // A node is judged where it is; any other tenant-plane resource is judged at its tenant's node.
        var node =
            resource as IResourceNode
            ?? ResourceNode.For(resource.TenantId, ResourceNodeTypes.Tenant, resource.TenantId);

        var openIdContext = HttpContextAccessor.HttpContext?.GetOpenIdContextOrDefault();
        if (openIdContext is null)
        {
            return;
        }

        // Creating a resource is authority at the parent node, which is the node passed here (ADR-1034, ADR-1411).
        var isCreate =
            requirement is OperationAuthorizationRequirement operation
            && string.Equals(operation.Name, Operations.Create.Name, StringComparison.Ordinal);

        // The principal and the node chain are read in the request's tenant scope.
        // See ADR-1411
        using var controlPlaneScope = AmbientTenantAccessor.BeginScope(
            openIdContext.Tenant.TenantId
        );

        await using var storeManager = await StoreManagerFactory.CreateAsync(
            CancellationToken.None
        );

        // Resolve the caller to a stable principal id (ADR-1035); a caller that has never been provisioned has no
        // authority, so there is nothing to look up.
        var principalId = await PrincipalResolver.ResolvePrincipalIdOrDefaultAsync(
            openIdContext,
            context.User,
            storeManager,
            CancellationToken.None
        );
        if (string.IsNullOrEmpty(principalId))
        {
            return;
        }

        var ancestorNodes = await GetAncestorNodesAsync(node, storeManager, CancellationToken.None);

        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignments = await store.GetByPrincipalAsync(principalId, CancellationToken.None);

        foreach (var assignment in assignments)
        {
            var isAuthorizingRole = isCreate
                ? IsCreationRole(assignment.RoleName)
                : IsManagementRole(assignment.RoleName);
            if (!isAuthorizingRole)
            {
                continue;
            }

            // An ancestor (platform, team or tenant) matches on type and identifier. Any other node's identifier is
            // unique only within its tenant, so an assignment at it counts only when it belongs to the node's tenant.
            // See ADR-1411
            var normalizedResourceId = Normalize(assignment.ResourceId);
            var isAncestor = ancestorNodes.Contains(
                (assignment.ResourceType, normalizedResourceId)
            );
            var isTheNode =
                string.Equals(assignment.ResourceType, node.ResourceType, StringComparison.Ordinal)
                && normalizedResourceId == Normalize(node.ResourceId)
                && Normalize(assignment.TenantId) == Normalize(node.TenantId);
            if (isAncestor || isTheNode)
            {
                context.Succeed(requirement);
                return;
            }
        }
    }

    // See ADR-1411
    private static async ValueTask<
        HashSet<(string ResourceType, string NormalizedResourceId)>
    > GetAncestorNodesAsync(
        IResourceNode resource,
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

        if (IsGloballyUniqueNodeType(resource.ResourceType))
        {
            nodes.Add((resource.ResourceType, Normalize(resource.ResourceId)));
        }

        string? teamId = null;
        if (string.Equals(resource.ResourceType, ResourceNodeTypes.Team, StringComparison.Ordinal))
        {
            teamId = resource.ResourceId;
        }
        else if (
            !string.Equals(
                resource.ResourceType,
                ResourceNodeTypes.Platform,
                StringComparison.Ordinal
            )
        )
        {
            nodes.Add((ResourceNodeTypes.Tenant, Normalize(resource.TenantId)));

            // An unknown tenant adds no team node.
            var tenantStore = storeManager.GetStore<ITenantStore>();
            var tenant = await tenantStore.GetOrDefaultAsync(resource.TenantId, cancellationToken);
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

    private static bool IsCreationRole(string roleName) =>
        string.Equals(roleName, BuiltInRoles.TeamAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.GlobalAdmin, StringComparison.OrdinalIgnoreCase);

    private static bool IsManagementRole(string roleName) =>
        string.Equals(roleName, BuiltInRoles.Owner, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.TeamAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.GlobalAdmin, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => value.ToLowerInvariant();
}
