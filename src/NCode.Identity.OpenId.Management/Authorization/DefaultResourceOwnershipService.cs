#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Security.Claims;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Provides the default implementation of <see cref="IResourceOwnershipService"/>.
/// </summary>
internal class DefaultResourceOwnershipService(
    ICryptoService cryptoService,
    IPrincipalResolver principalResolver
) : IResourceOwnershipService
{
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;

    /// <inheritdoc />
    public async ValueTask AssignCreatorAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        // Resolve (and provision on first sight) the stable principal id for the creator (ADR-0035).
        var principalId = await PrincipalResolver.ResolvePrincipalIdAsync(
            openIdContext,
            user,
            storeManager,
            cancellationToken
        );

        var store = storeManager.GetStore<IRoleAssignmentStore>();
        await store.AddAsync(
            new PersistedRoleAssignment
            {
                TenantId = tenantId,
                AssignmentId = CryptoService.GenerateResourceId(),
                PrincipalId = principalId,
                RoleName = BuiltInRoles.Owner,
                ResourceType = resourceType,
                ResourceId = resourceId,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetOwnersAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignments = await store.GetByResourceAsync(
            resourceType,
            resourceId,
            cancellationToken
        );
        return assignments.Where(IsOwner).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<PersistedRoleAssignment> AddOwnerAsync(
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        string principalId,
        CancellationToken cancellationToken
    )
    {
        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignments = await store.GetByResourceAsync(
            resourceType,
            resourceId,
            cancellationToken
        );

        var existing = assignments.FirstOrDefault(assignment =>
            IsOwner(assignment)
            && string.Equals(assignment.PrincipalId, principalId, StringComparison.Ordinal)
        );
        if (existing is not null)
        {
            return existing;
        }

        var owner = new PersistedRoleAssignment
        {
            TenantId = tenantId,
            AssignmentId = CryptoService.GenerateResourceId(),
            PrincipalId = principalId,
            RoleName = BuiltInRoles.Owner,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ConcurrencyToken = string.Empty,
        };
        await store.AddAsync(owner, cancellationToken);
        return owner;
    }

    /// <inheritdoc />
    public async ValueTask<OwnerRemovalResult> RemoveOwnerAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        string principalId,
        CancellationToken cancellationToken
    )
    {
        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var owners = (await store.GetByResourceAsync(resourceType, resourceId, cancellationToken))
            .Where(IsOwner)
            .ToList();

        var toRemove = owners
            .Where(assignment =>
                string.Equals(assignment.PrincipalId, principalId, StringComparison.Ordinal)
            )
            .ToList();
        if (toRemove.Count == 0)
        {
            return OwnerRemovalResult.NotFound;
        }

        // Refuse to orphan the resource; a tenant or global admin can always reassign ownership (ADR-0034).
        if (owners.Count - toRemove.Count < 1)
        {
            return OwnerRemovalResult.LastOwnerForbidden;
        }

        foreach (var assignment in toRemove)
        {
            await store.RemoveAsync(assignment.AssignmentId, cancellationToken);
        }

        return OwnerRemovalResult.Removed;
    }

    private static bool IsOwner(PersistedRoleAssignment assignment) =>
        string.Equals(assignment.RoleName, BuiltInRoles.Owner, StringComparison.OrdinalIgnoreCase);
}
