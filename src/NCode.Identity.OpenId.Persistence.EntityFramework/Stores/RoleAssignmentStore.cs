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

using System.Linq.Expressions;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IRoleAssignmentStore"/> that uses Entity Framework Core for
/// persistence.
/// </summary>
internal class RoleAssignmentStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStore<PersistedRoleAssignment, RoleAssignmentEntity>, IRoleAssignmentStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    protected override ValueTask<PersistedRoleAssignment> MapFromEntityAsync(
        RoleAssignmentEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedRoleAssignment
            {
                TenantId = entity.Tenant.TenantId,
                AssignmentId = entity.AssignmentId,
                PrincipalId = entity.PrincipalId,
                RoleName = entity.RoleName,
                ResourceType = entity.ResourceType,
                ResourceId = entity.ResourceId,
                ConcurrencyToken = entity.ConcurrencyToken,
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<RoleAssignmentEntity?> GetEntityOrDefaultAsync(
        Expression<Func<RoleAssignmentEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .RoleAssignments.Include(entity => entity.Tenant)
                .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<RoleAssignmentEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext.RoleAssignments.Include(entity => entity.Tenant).AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(RoleAssignmentEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedRoleAssignment assignment,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityAsync(assignment.TenantId, cancellationToken);

        // The interceptor leaves a pre-seeded insert token intact (ADR-0012).
        assignment.ConcurrencyToken = NextConcurrencyToken();

        var entity = new RoleAssignmentEntity
        {
            Id = NextId(),
            TenantId = tenantEntity.Id,
            AssignmentId = assignment.AssignmentId,
            NormalizedAssignmentId = Normalize(assignment.AssignmentId),
            PrincipalId = assignment.PrincipalId,
            NormalizedPrincipalId = Normalize(assignment.PrincipalId),
            RoleName = assignment.RoleName,
            NormalizedRoleName = Normalize(assignment.RoleName),
            ResourceType = assignment.ResourceType,
            ResourceId = assignment.ResourceId,
            NormalizedResourceId = Normalize(assignment.ResourceId),
            ConcurrencyToken = assignment.ConcurrencyToken,
            Tenant = tenantEntity,
        };

        await DbContext.RoleAssignments.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedRoleAssignment?> GetOrDefaultAsync(
        string assignmentId,
        CancellationToken cancellationToken
    )
    {
        var normalizedAssignmentId = Normalize(assignmentId);
        var entity = await GetEntityOrDefaultAsync(
            assignment => assignment.NormalizedAssignmentId == normalizedAssignmentId,
            cancellationToken
        );
        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string assignmentId, CancellationToken cancellationToken)
    {
        var normalizedAssignmentId = Normalize(assignmentId);
        var entity = await GetEntityOrDefaultAsync(
            assignment => assignment.NormalizedAssignmentId == normalizedAssignmentId,
            cancellationToken
        );
        if (entity is null)
        {
            return;
        }

        DbContext.RoleAssignments.Remove(entity);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetByResourceAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var normalizedResourceId = Normalize(resourceId);
        var entities = await DbContext
            .RoleAssignments.Include(entity => entity.Tenant)
            .Where(entity =>
                entity.ResourceType == resourceType
                && entity.NormalizedResourceId == normalizedResourceId
            )
            .ToListAsync(cancellationToken);

        return await MapAllAsync(entities, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetByPrincipalAsync(
        string principalId,
        CancellationToken cancellationToken
    )
    {
        var normalizedPrincipalId = Normalize(principalId);
        var entities = await DbContext
            .RoleAssignments.Include(entity => entity.Tenant)
            .Where(entity => entity.NormalizedPrincipalId == normalizedPrincipalId)
            .ToListAsync(cancellationToken);

        return await MapAllAsync(entities, cancellationToken);
    }

    private async ValueTask<IReadOnlyList<PersistedRoleAssignment>> MapAllAsync(
        List<RoleAssignmentEntity> entities,
        CancellationToken cancellationToken
    )
    {
        var result = new List<PersistedRoleAssignment>(entities.Count);
        foreach (var entity in entities)
        {
            result.Add(await MapFromEntityAsync(entity, cancellationToken));
        }

        return result;
    }
}
