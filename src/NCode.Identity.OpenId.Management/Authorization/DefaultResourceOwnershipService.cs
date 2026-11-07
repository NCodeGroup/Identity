// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides the default implementation of <see cref="IResourceOwnershipService"/>.
/// </summary>
internal class DefaultResourceOwnershipService(
    ICryptoService cryptoService,
    ICallerPrincipalResolver callerPrincipalResolver,
    IAmbientTenantAccessor ambientTenantAccessor
) : IResourceOwnershipService
{
    private ICryptoService CryptoService { get; } = cryptoService;
    private ICallerPrincipalResolver CallerPrincipalResolver { get; } = callerPrincipalResolver;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<PersistedRoleAssignment?> AssignCreatorAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        // Resolve (and provision on first sight) the stable principal id for the creator (ADR-1035). Principals and
        // their federated identities live in the control-plane tenant, whatever tenant the request is scoped to.
        // See ADR-1411
        string principalId;
        using (AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId))
        {
            principalId = await CallerPrincipalResolver.ResolveCallerPrincipalIdAsync(
                openIdContext,
                user,
                storeManager,
                cancellationToken
            );
        }

        // A caller that resolves to no principal becomes no owner.
        if (string.IsNullOrEmpty(principalId))
        {
            return null;
        }

        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignment = new PersistedRoleAssignment
        {
            TenantId = tenantId,
            AssignmentId = CryptoService.GenerateResourceId(),
            PrincipalId = principalId,
            RoleName = BuiltInRoles.Owner,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ConcurrencyToken = string.Empty,
        };
        await store.AddAsync(assignment, cancellationToken);
        return assignment;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetOwnersAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    ) =>
        GetAssignmentsAsync(
            storeManager,
            resourceType,
            resourceId,
            BuiltInRoles.Owner,
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<RoleAssignmentGrant> AddOwnerAsync(
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        string principalId,
        CancellationToken cancellationToken
    ) =>
        AddAssignmentAsync(
            storeManager,
            tenantId,
            resourceType,
            resourceId,
            principalId,
            BuiltInRoles.Owner,
            cancellationToken
        );

    /// <inheritdoc />
    public async ValueTask<OwnerRemoval> RemoveOwnerAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        string principalId,
        CancellationToken cancellationToken
    )
    {
        var owners = await GetOwnersAsync(
            storeManager,
            resourceType,
            resourceId,
            cancellationToken
        );

        var toRemoveCount = owners.Count(assignment =>
            string.Equals(assignment.PrincipalId, principalId, StringComparison.Ordinal)
        );
        if (toRemoveCount == 0)
        {
            return new OwnerRemoval
            {
                Result = OwnerRemovalResult.NotFound,
                RemovedAssignments = [],
            };
        }

        // Refuse to orphan the resource; a tenant or global admin can always reassign ownership (ADR-1034).
        if (owners.Count - toRemoveCount < 1)
        {
            return new OwnerRemoval
            {
                Result = OwnerRemovalResult.LastOwnerForbidden,
                RemovedAssignments = [],
            };
        }

        var removedAssignments = await RemoveAssignmentAsync(
            storeManager,
            resourceType,
            resourceId,
            principalId,
            BuiltInRoles.Owner,
            cancellationToken
        );

        return new OwnerRemoval
        {
            Result = OwnerRemovalResult.Removed,
            RemovedAssignments = removedAssignments,
        };
    }

    // See ADR-1411
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetAssignmentsAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignments = await store.GetByResourceAsync(
            resourceType,
            resourceId,
            cancellationToken
        );
        return assignments.Where(assignment => HasRole(assignment, roleName)).ToList();
    }

    // See ADR-1411
    /// <inheritdoc />
    public async ValueTask<RoleAssignmentGrant> AddAssignmentAsync(
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        string principalId,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(principalId);

        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignments = await store.GetByResourceAsync(
            resourceType,
            resourceId,
            cancellationToken
        );

        var existing = assignments.FirstOrDefault(assignment =>
            HasRole(assignment, roleName)
            && string.Equals(assignment.PrincipalId, principalId, StringComparison.Ordinal)
        );
        if (existing is not null)
        {
            return new RoleAssignmentGrant { Assignment = existing, IsAdded = false };
        }

        var created = new PersistedRoleAssignment
        {
            TenantId = tenantId,
            AssignmentId = CryptoService.GenerateResourceId(),
            PrincipalId = principalId,
            RoleName = roleName,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ConcurrencyToken = string.Empty,
        };
        await store.AddAsync(created, cancellationToken);
        return new RoleAssignmentGrant { Assignment = created, IsAdded = true };
    }

    // See ADR-1411
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedRoleAssignment>> RemoveAssignmentAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        string principalId,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var toRemove = (await store.GetByResourceAsync(resourceType, resourceId, cancellationToken))
            .Where(assignment =>
                HasRole(assignment, roleName)
                && string.Equals(assignment.PrincipalId, principalId, StringComparison.Ordinal)
            )
            .ToList();

        foreach (var assignment in toRemove)
        {
            await store.RemoveAsync(assignment.AssignmentId, cancellationToken);
        }

        return toRemove;
    }

    private static bool HasRole(PersistedRoleAssignment assignment, string roleName) =>
        string.Equals(assignment.RoleName, roleName, StringComparison.OrdinalIgnoreCase);
}
