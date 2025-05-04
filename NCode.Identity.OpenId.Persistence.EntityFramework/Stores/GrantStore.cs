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
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IGrantStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
public class GrantStore(
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
        return ValueTask.FromResult(new PersistedGrant
        {
            GrantType = entity.GrantType,
            HashedKey = entity.HashedKey,
            ConcurrencyToken = entity.ConcurrencyToken,
            TenantId = entity.Tenant?.TenantId,
            ClientId = entity.Client?.ClientId,
            SubjectId = entity.SubjectId,
            CreatedWhen = entity.CreatedWhen,
            ExpiresWhen = entity.ExpiresWhen,
            RevokedWhen = entity.RevokedWhen,
            ConsumedWhen = entity.ConsumedWhen,
            PayloadJson = entity.PayloadJson,
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<GrantEntity?> GetEntityOrDefaultAsync(
        Expression<Func<GrantEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Grants
            .Include(entity => entity.Tenant)
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
            entity =>
                entity.GrantType == grantType &&
                entity.HashedKey == hashedKey,
            cancellationToken
        );

        if (grantEntity is null)
        {
            return null;
        }

        return await MapFromEntityAsync(grantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedGrant persistedGrant,
        CancellationToken cancellationToken
    )
    {
        TenantEntity? tenantEntity = null;
        if (!string.IsNullOrEmpty(persistedGrant.TenantId))
        {
            tenantEntity = await GetTenantEntityOrDefaultAsync(persistedGrant.TenantId, cancellationToken);
        }

        ClientEntity? clientEntity = null;
        if (!string.IsNullOrEmpty(persistedGrant.ClientId))
        {
            if (tenantEntity is null)
            {
                throw new InvalidOperationException("TenantId is required when ClientId is specified.");
            }

            var normalizedClientId = Normalize(persistedGrant.ClientId);
            clientEntity = await DbContext.Clients.FirstOrDefaultAsync(
                entity =>
                    entity.TenantId == tenantEntity.Id &&
                    entity.NormalizedClientId == normalizedClientId,
                cancellationToken
            );

            if (clientEntity is null)
                throw new InvalidOperationException($"Client '{persistedGrant.ClientId}' not found.");
        }

        persistedGrant.ConcurrencyToken = NextConcurrencyToken();

        var grantEntity = new GrantEntity
        {
            Id = NextId(),
            GrantType = persistedGrant.GrantType,
            HashedKey = persistedGrant.HashedKey,
            ConcurrencyToken = persistedGrant.ConcurrencyToken,
            TenantId = tenantEntity?.Id,
            ClientId = clientEntity?.Id,
            SubjectId = persistedGrant.SubjectId,
            NormalizedSubjectId = Normalize(persistedGrant.SubjectId),
            CreatedWhen = persistedGrant.CreatedWhen.ToUniversalTime(),
            ExpiresWhen = persistedGrant.ExpiresWhen?.ToUniversalTime(),
            RevokedWhen = persistedGrant.RevokedWhen?.ToUniversalTime(),
            ConsumedWhen = persistedGrant.ConsumedWhen?.ToUniversalTime(),
            PayloadJson = persistedGrant.PayloadJson,
            Tenant = tenantEntity,
            Client = clientEntity
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
                entity.GrantType == persistedGrant.GrantType &&
                entity.HashedKey == persistedGrant.HashedKey,
            cancellationToken
        );

        if (grantEntity is null)
        {
            throw new InvalidOperationException("The specified grant was not found.");
        }

        if (!string.Equals(persistedGrant.ConcurrencyToken, grantEntity.ConcurrencyToken, StringComparison.Ordinal))
        {
            throw new DbUpdateConcurrencyException(
                "The OpenId Grant has been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        grantEntity.ConcurrencyToken = nextConcurrencyToken;
        grantEntity.ExpiresWhen = persistedGrant.ExpiresWhen;
        grantEntity.RevokedWhen = persistedGrant.RevokedWhen;
        grantEntity.ConsumedWhen = persistedGrant.ConsumedWhen;

        DbContext.Grants.Update(grantEntity);

        persistedGrant.ConcurrencyToken = nextConcurrencyToken;
    }
}
