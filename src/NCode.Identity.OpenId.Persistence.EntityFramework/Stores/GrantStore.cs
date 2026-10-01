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
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IGrantStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
internal class GrantStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStore<PersistedGrant, GrantEntity>, IGrantStore
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
        string? subjectId,
        string? clientId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var afterId = DecodeCursor(cursor);

        var query = DbContext
            .Grants.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        query = ApplyGrantFilters(query, subjectId, clientId);

        // Fetch one extra row so a full page signals that another page exists.
        var entities = await query
            .OrderBy(entity => entity.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await BuildPageAsync(entities, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<long> RevokeWhereAsync(
        string? subjectId,
        string? clientId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        // Only active grants are touched so the operation is idempotent and the interceptor bumps the row token
        // (ADR-0012) on exactly the rows it changes.
        var query = DbContext
            .Grants.Include(entity => entity.Client)
            .Where(entity => entity.RevokedWhen == null);

        query = ApplyGrantFilters(query, subjectId, clientId);

        var entities = await query.ToListAsync(cancellationToken);

        var utcRevokedWhen = revokedWhen.ToUniversalTime();
        foreach (var entity in entities)
        {
            entity.RevokedWhen = utcRevokedWhen;
        }

        return entities.Count;
    }

    private static IQueryable<GrantEntity> ApplyGrantFilters(
        IQueryable<GrantEntity> query,
        string? subjectId,
        string? clientId
    )
    {
        if (!string.IsNullOrEmpty(subjectId))
        {
            var normalizedSubjectId = Normalize(subjectId);
            query = query.Where(entity => entity.NormalizedSubjectId == normalizedSubjectId);
        }

        if (!string.IsNullOrEmpty(clientId))
        {
            var normalizedClientId = Normalize(clientId);
            query = query.Where(entity =>
                entity.Client != null && entity.Client.NormalizedClientId == normalizedClientId
            );
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
            throw new DbUpdateConcurrencyException(
                "The OpenId Grant has been modified by another process. Please reload and try again."
            );
        }

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-0012).
        grantEntity.ExpiresWhen = persistedGrant.ExpiresWhen;
        grantEntity.RevokedWhen = persistedGrant.RevokedWhen;
        grantEntity.ConsumedWhen = persistedGrant.ConsumedWhen;

        DbContext.Grants.Update(grantEntity);
    }
}
