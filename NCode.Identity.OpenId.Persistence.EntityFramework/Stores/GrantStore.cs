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
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Models;
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
    ICryptoService cryptoService,
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithEntityId<PersistedGrant, GrantEntity>, IGrantStore
{
    // This uses C# compiler's ability to refer to static data directly.
    // For more information see https://vcsjones.dev/2019/02/01/csharp-readonly-span-bytes-static
    private static ReadOnlySpan<char> TenantDelimiter => "~~";

    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    public override bool IsRemoveSupported => true;

    private static string GetHashInput(PersistedGrantId grantId) =>
        string.IsNullOrEmpty(grantId.TenantId) ?
            grantId.GrantKey :
            string.Concat(grantId.TenantId.AsSpan(), TenantDelimiter, grantId.GrantKey.AsSpan());

    private string GetHashedKey(PersistedGrantId grantId) =>
        CryptoService.HashValue(
            GetHashInput(grantId),
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64
        );

    /// <inheritdoc />
    protected override ValueTask<PersistedGrant> MapFromEntityAsync(
        GrantEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedGrant
        {
            Id = entity.Id,
            GrantType = entity.GrantType,
            HashedKey = entity.HashedKey,
            TenantId = entity.Tenant?.TenantId,
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
    public async ValueTask<PersistedGrant?> TryGetAsync(
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    )
    {
        long? tenantId = null;
        if (!string.IsNullOrEmpty(grantId.TenantId))
        {
            tenantId = await TryGetTenantIdAsync(grantId.TenantId, cancellationToken);
            if (tenantId is null)
            {
                return null;
            }
        }

        var hashedKey = GetHashedKey(grantId);

        var entity = await TryGetEntityAsync(
            entity =>
                entity.GrantType == grantId.GrantType &&
                entity.HashedKey == hashedKey,
            cancellationToken
        );

        if (entity is null || entity.TenantId != tenantId)
            return null;

        return await MapFromEntityAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedGrant persistedGrant,
        CancellationToken cancellationToken
    )
    {
        TenantEntity? tenantEntity = null;
        if (!string.IsNullOrEmpty(persistedGrant.TenantId))
        {
            tenantEntity = await GetTenantAsync(persistedGrant, cancellationToken);
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

        persistedGrant.Id = NextId(persistedGrant.Id);

        var grantEntity = new GrantEntity
        {
            Id = persistedGrant.Id,
            GrantType = persistedGrant.GrantType,
            HashedKey = persistedGrant.HashedKey,
            ConcurrencyToken = NextConcurrencyToken(),
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
    public override async ValueTask UpdateAsync(
        PersistedGrant persistedGrant,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityByIdAsync(persistedGrant.Id, cancellationToken);

        entity.ExpiresWhen = persistedGrant.ExpiresWhen;
        entity.RevokedWhen = persistedGrant.RevokedWhen;
        entity.ConsumedWhen = persistedGrant.ConsumedWhen;

        entity.ConcurrencyToken = NextConcurrencyToken();

        DbContext.Grants.Update(entity);
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

        grant.ConcurrencyToken = NextConcurrencyToken();

        DbContext.Grants.Remove(grant);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant?> TryGetAsync(
        string grantType,
        string hashedKey,
        CancellationToken cancellationToken
    )
    {
        return await TryGetAsync(
            entity =>
                entity.GrantType == grantType &&
                entity.HashedKey == hashedKey,
            cancellationToken
        );
    }
}
