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
/// tenant's and the team's assignments. A principal holds authority when it has a role assignment at that node or an ancestor (its tenant, its
/// team, or the platform root). This realizes ownership: an <c>Owner</c> assignment at an instance lets the principal
/// manage that instance (and everything beneath it) without any tenant-wide role. Ownership grants every operation
/// except <see cref="Operations.Create"/>; creating a new resource is authority at the parent node, held by a
/// <c>TeamAdmin</c> or <c>GlobalAdmin</c> assignment at the node or an ancestor.
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

        // Creating a resource is authority at the parent node, which is the node passed here (ADR-1034, ADR-1403).
        var isCreate =
            requirement is OperationAuthorizationRequirement operation
            && string.Equals(operation.Name, Operations.Create.Name, StringComparison.Ordinal);

        // The principal and the node chain are read in the request's tenant scope.
        // See ADR-1403
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
            if (
                isAuthorizingRole
                && ancestorNodes.Contains(
                    (assignment.ResourceType, Normalize(assignment.ResourceId))
                )
            )
            {
                context.Succeed(requirement);
                return;
            }
        }
    }

    // See ADR-1403
    private static async ValueTask<
        HashSet<(string ResourceType, string NormalizedResourceId)>
    > GetAncestorNodesAsync(
        IResourceNode resource,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        // A resource's authority chain is fixed: the node itself, its tenant, its team, then the platform root.
        HashSet<(string ResourceType, string NormalizedResourceId)> nodes =
        [
            (resource.ResourceType, Normalize(resource.ResourceId)),
            (ResourceNodeTypes.Platform, Normalize(OpenIdConstants.PlatformResourceId)),
        ];

        if (string.Equals(resource.ResourceType, ResourceNodeTypes.Team, StringComparison.Ordinal))
        {
            nodes.Add((ResourceNodeTypes.Team, Normalize(resource.ResourceId)));
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
            if (tenant is not null)
            {
                nodes.Add((ResourceNodeTypes.Team, Normalize(tenant.TeamId)));
            }
        }

        return nodes;
    }

    private static bool IsCreationRole(string roleName) =>
        string.Equals(roleName, BuiltInRoles.TeamAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.GlobalAdmin, StringComparison.OrdinalIgnoreCase);

    private static bool IsManagementRole(string roleName) =>
        string.Equals(roleName, BuiltInRoles.Owner, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.TeamAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.GlobalAdmin, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => value.ToLowerInvariant();
}
