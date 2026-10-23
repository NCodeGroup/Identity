#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IGrantStore"/> that uses Entity Framework Core for persistence.
/// </summary>
internal class GrantStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : CoreBaseStore<PersistedGrant, GrantEntity>, IGrantStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    protected override ValueTask<PersistedGrant> MapFromEntityAsync(
        GrantEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedGrant
            {
                GrantType = entity.GrantType,
                HashedKey = entity.HashedKey,
                GrantId = entity.GrantId,
                ConcurrencyToken = entity.ConcurrencyToken,
                TenantId = entity.Tenant.TenantId,
                ClientId = entity.Client?.ClientId,
                SubjectId = entity.SubjectId,
                SessionId = entity.SessionId,
                RefreshTokenLineageId = entity.RefreshTokenLineageId,
                CreatedWhen = entity.CreatedWhen,
                ExpiresWhen = entity.ExpiresWhen,
                RevokedWhen = entity.RevokedWhen,
                ConsumedWhen = entity.ConsumedWhen,
                PayloadJson = entity.PayloadJson,
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<GrantEntity?> GetEntityOrDefaultAsync(
        Expression<Func<GrantEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .Grants.Include(entity => entity.Tenant)
                .Include(entity => entity.Client)
                .SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant?> GetOrDefaultAsync(
        string grantType,
        string hashedKey,
        CancellationToken cancellationToken
    )
    {
        var grantEntity = await GetEntityOrDefaultAsync(
            entity => entity.GrantType == grantType && entity.HashedKey == hashedKey,
            cancellationToken
        );

        if (grantEntity is null)
        {
            return null;
        }

        return await MapFromEntityAsync(grantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant?> GetOrDefaultAsync(
        string grantId,
        CancellationToken cancellationToken
    )
    {
        var normalizedGrantId = Normalize(grantId);
        var grantEntity = await GetEntityOrDefaultAsync(
            entity => entity.NormalizedGrantId == normalizedGrantId,
            cancellationToken
        );

        if (grantEntity is null)
        {
            return null;
        }

        return await MapFromEntityAsync(grantEntity, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<GrantEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext
            .Grants.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(GrantEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedGrant>> GetPageAsync(
        GrantFilter filter,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var afterId = DecodeCursor(page.Cursor);

        var query = DbContext
            .Grants.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        query = ApplyGrantFilters(query, filter);

        // Fetch one extra row so a full page signals that another page exists.
        var entities = await query
            .OrderBy(entity => entity.Id)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken);

        return await BuildPageAsync(entities, page.Limit, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedGrant>> GetUnrevokedExpiredPageAsync(
        string grantType,
        DateTimeOffset expiredAt,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var afterId = DecodeCursor(page.Cursor);

        var query = DbContext
            .Grants.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        // The sessions that ended while no request used them. See ADR-0223.
        var cutoff = expiredAt.ToUniversalTime();
        query = query.Where(entity =>
            entity.GrantType == grantType
            && entity.RevokedWhen == null
            && (entity.ExpiresWhen == null || entity.ExpiresWhen <= cutoff)
            && !entity.Tenant.IsDisabled
        );

        // Fetch one extra row so a full page signals that another page exists.
        var entities = await query
            .OrderBy(entity => entity.Id)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken);

        return await BuildPageAsync(entities, page.Limit, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<long> RevokeWhereAsync(
        GrantFilter filter,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        // Only active grants are touched so the operation is idempotent and the interceptor bumps the row token
        // (ADR-0012) on exactly the rows it changes.
        var query = DbContext
            .Grants.Include(entity => entity.Client)
            .Where(entity => entity.RevokedWhen == null);

        query = ApplyGrantFilters(query, filter);

        var entities = await query.ToListAsync(cancellationToken);

        var utcRevokedWhen = revokedWhen.ToUniversalTime();
        foreach (var entity in entities)
        {
            entity.RevokedWhen = utcRevokedWhen;
        }

        return entities.Count;
    }

    /// <inheritdoc />
    public async ValueTask<long> RevokeByRefreshTokenLineageAsync(
        string refreshTokenLineageId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        // Only active grants are touched so the operation is idempotent and the interceptor bumps the row token
        // (ADR-0012) on exactly the rows it changes. The lineage id is an opaque resource id matched verbatim.
        var entities = await DbContext
            .Grants.Where(entity =>
                entity.RevokedWhen == null && entity.RefreshTokenLineageId == refreshTokenLineageId
            )
            .ToListAsync(cancellationToken);

        var utcRevokedWhen = revokedWhen.ToUniversalTime();
        foreach (var entity in entities)
        {
            entity.RevokedWhen = utcRevokedWhen;
        }

        return entities.Count;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedGrant>> GetActiveByRefreshTokenLineageAsync(
        string refreshTokenLineageId,
        CancellationToken cancellationToken
    )
    {
        // The grants are tracked, so the caller can update them in the same unit of work. The lineage id is an opaque
        // resource id matched verbatim.
        var entities = await DbContext
            .Grants.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .Where(entity =>
                entity.RevokedWhen == null && entity.RefreshTokenLineageId == refreshTokenLineageId
            )
            .ToListAsync(cancellationToken);

        var grants = new List<PersistedGrant>(entities.Count);
        foreach (var entity in entities)
        {
            grants.Add(await MapFromEntityAsync(entity, cancellationToken));
        }

        return grants;
    }

    /// <inheritdoc />
    // See ADR-0218.
    public async ValueTask<int> RemoveEndedBeforeAsync(
        DateTimeOffset endedBefore,
        IReadOnlyCollection<string> grantTypesEndedByRevocation,
        int limit,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(grantTypesEndedByRevocation);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var cutoff = endedBefore.ToUniversalTime();
        var endedByRevocation = grantTypesEndedByRevocation.ToList();

        // The prune never removes a session that was not terminated. See ADR-0223.
        var entities = await DbContext
            .Grants.Where(entity =>
                entity.RevokedWhen < cutoff
                || (entity.ExpiresWhen < cutoff && !endedByRevocation.Contains(entity.GrantType))
            )
            .OrderBy(entity => entity.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        DbContext.Grants.RemoveRange(entities);

        return entities.Count;
    }

    private static IQueryable<GrantEntity> ApplyGrantFilters(
        IQueryable<GrantEntity> query,
        GrantFilter filter
    )
    {
        if (!string.IsNullOrEmpty(filter.SubjectId))
        {
            var normalizedSubjectId = Normalize(filter.SubjectId);
            query = query.Where(entity => entity.NormalizedSubjectId == normalizedSubjectId);
        }

        if (!string.IsNullOrEmpty(filter.ClientId))
        {
            var normalizedClientId = Normalize(filter.ClientId);
            query = query.Where(entity =>
                entity.Client != null && entity.Client.NormalizedClientId == normalizedClientId
            );
        }

        // The sid is an opaque, case-sensitive key, so it is matched verbatim.
        if (!string.IsNullOrEmpty(filter.SessionId))
        {
            query = query.Where(entity => entity.SessionId == filter.SessionId);
        }

        // The grant id is the opaque public identifier, which the store matches by its normalized form.
        if (!string.IsNullOrEmpty(filter.GrantId))
        {
            var normalizedGrantId = Normalize(filter.GrantId);
            query = query.Where(entity => entity.NormalizedGrantId == normalizedGrantId);
        }

        // The grant type is a fixed protocol constant, so it is matched verbatim.
        if (!string.IsNullOrEmpty(filter.GrantType))
        {
            query = query.Where(entity => entity.GrantType == filter.GrantType);
        }

        return query;
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedGrant persistedGrant,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity =
            await GetTenantEntityOrDefaultAsync(persistedGrant.TenantId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Tenant '{persistedGrant.TenantId}' not found."
            );

        ClientEntity? clientEntity = null;
        if (!string.IsNullOrEmpty(persistedGrant.ClientId))
        {
            var normalizedClientId = Normalize(persistedGrant.ClientId);
            clientEntity = await DbContext.Clients.FirstOrDefaultAsync(
                entity =>
                    entity.TenantId == tenantEntity.Id
                    && entity.NormalizedClientId == normalizedClientId,
                cancellationToken
            );

            if (clientEntity is null)
                throw new InvalidOperationException(
                    $"Client '{persistedGrant.ClientId}' not found."
                );
        }

        // The opaque GrantId is assigned by the grant-creation service (ICryptoService.GenerateResourceId).
        var grantEntity = new GrantEntity
        {
            Id = NextId(),
            GrantId = persistedGrant.GrantId,
            NormalizedGrantId = Normalize(persistedGrant.GrantId),
            GrantType = persistedGrant.GrantType,
            HashedKey = persistedGrant.HashedKey,
            ConcurrencyToken = string.Empty,
            TenantId = tenantEntity.Id,
            NormalizedTenantId = tenantEntity.NormalizedTenantId,
            ClientId = clientEntity?.Id,
            SubjectId = persistedGrant.SubjectId,
            NormalizedSubjectId = Normalize(persistedGrant.SubjectId),
            SessionId = persistedGrant.SessionId,
            RefreshTokenLineageId = persistedGrant.RefreshTokenLineageId,
            CreatedWhen = persistedGrant.CreatedWhen.ToUniversalTime(),
            ExpiresWhen = persistedGrant.ExpiresWhen?.ToUniversalTime(),
            RevokedWhen = persistedGrant.RevokedWhen?.ToUniversalTime(),
            ConsumedWhen = persistedGrant.ConsumedWhen?.ToUniversalTime(),
            PayloadJson = persistedGrant.PayloadJson,
            Tenant = tenantEntity,
            Client = clientEntity,
        };

        await DbContext.Grants.AddAsync(grantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        PersistedGrant persistedGrant,
        CancellationToken cancellationToken
    )
    {
        var grantEntity = await GetEntityOrDefaultAsync(
            entity =>
                entity.GrantType == persistedGrant.GrantType
                && entity.HashedKey == persistedGrant.HashedKey,
            cancellationToken
        );

        if (grantEntity is null)
        {
            throw new InvalidOperationException("The specified grant was not found.");
        }

        if (
            !string.Equals(
                persistedGrant.ConcurrencyToken,
                grantEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                "The OpenId Grant has been modified by another process. Please reload and try again."
            );
        }

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-0012).
        grantEntity.ExpiresWhen = persistedGrant.ExpiresWhen;
        grantEntity.RevokedWhen = persistedGrant.RevokedWhen;
        grantEntity.ConsumedWhen = persistedGrant.ConsumedWhen;
        grantEntity.PayloadJson = persistedGrant.PayloadJson;

        DbContext.Grants.Update(grantEntity);
    }
}
