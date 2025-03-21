﻿#region Copyright Preamble

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
) : BaseStoreWithEntityId<PersistedGrant, GrantEntity>, IGrantStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    public override bool IsRemoveSupported => true;

    /// <inheritdoc />
    protected override ValueTask<PersistedGrant> MapAsync(
        GrantEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedGrant
        {
            Id = entity.Id,
            TenantId = entity.Tenant.TenantId,
            GrantType = entity.GrantType,
            HashedKey = entity.HashedKey,
            ClientId = entity.Client?.ClientId,
            SubjectId = entity.SubjectId,
            CreatedWhen = entity.CreatedWhen,
            ExpiresWhen = entity.ExpiresWhen,
            RevokedWhen = entity.RevokedWhen,
            ConsumedWhen = entity.ConsumedWhen,
            PayloadJson = entity.PayloadJson
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<GrantEntity?> TryGetEntityAsync(
        Expression<Func<GrantEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Grants
            .Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedGrant persistedGrant,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantAsync(persistedGrant, cancellationToken);

        persistedGrant.Id = NextId(persistedGrant.Id);

        var normalizedClientId = Normalize(persistedGrant.ClientId);

        var clientEntity = await DbContext.Clients.FirstOrDefaultAsync(
            entity =>
                entity.TenantId == tenantEntity.Id &&
                entity.NormalizedClientId == normalizedClientId,
            cancellationToken
        );

        if (persistedGrant.ClientId is not null && clientEntity is null)
            throw new InvalidOperationException($"Client '{persistedGrant.ClientId}' not found.");

        var grantEntity = new GrantEntity
        {
            Id = persistedGrant.Id,
            TenantId = tenantEntity.Id,
            GrantType = persistedGrant.GrantType,
            HashedKey = persistedGrant.HashedKey,
            ConcurrencyToken = NextConcurrencyToken(),
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
    public override async ValueTask RemoveByIdAsync(
        long id,
        CancellationToken cancellationToken
    )
    {
        var grant = await TryGetEntityAsync(entity => entity.Id == id, cancellationToken);
        if (grant is null)
            return;

        DbContext.Grants.Remove(grant);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant?> TryGetAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);

        return await TryGetAsync(
            entity =>
                entity.Tenant.NormalizedTenantId == normalizedTenantId &&
                entity.GrantType == grantType &&
                entity.HashedKey == hashedKey,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask SetConsumedOnceAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);

        var entity = await TryGetEntityAsync(
            entity =>
                entity.Tenant.NormalizedTenantId == normalizedTenantId &&
                entity.GrantType == grantType &&
                entity.HashedKey == hashedKey,
            cancellationToken
        );

        if (entity is null)
            return;

        if (entity.RevokedWhen is not null)
            return;

        if (entity.ConsumedWhen is not null)
            return;

        entity.ConcurrencyToken = NextConcurrencyToken();
        entity.ConsumedWhen = consumedWhen.ToUniversalTime();
    }

    /// <inheritdoc />
    public async ValueTask SetRevokedOnceAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);

        var entity = await TryGetEntityAsync(
            entity =>
                entity.Tenant.NormalizedTenantId == normalizedTenantId &&
                entity.GrantType == grantType &&
                entity.HashedKey == hashedKey,
            cancellationToken
        );

        if (entity is null)
            return;

        if (entity.RevokedWhen is not null)
            return;

        entity.ConcurrencyToken = NextConcurrencyToken();
        entity.RevokedWhen = revokedWhen.ToUniversalTime();
    }

    /// <inheritdoc />
    public async ValueTask UpdateExpirationAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);

        var entity = await TryGetEntityAsync(
            entity =>
                entity.Tenant.NormalizedTenantId == normalizedTenantId &&
                entity.GrantType == grantType &&
                entity.HashedKey == hashedKey,
            cancellationToken
        );

        if (entity is null)
            return;

        if (entity.RevokedWhen is not null)
            return;

        entity.ConcurrencyToken = NextConcurrencyToken();
        entity.ExpiresWhen = expiresWhen.ToUniversalTime();
    }
}
