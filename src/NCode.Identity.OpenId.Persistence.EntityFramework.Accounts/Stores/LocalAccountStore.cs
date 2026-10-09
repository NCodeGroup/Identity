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
using NCode.Identity.Json;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Entities;
using NCode.Identity.OpenId.Persistence.EntityFramework.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores;

/// <summary>
/// Provides the Entity Framework Core implementation of <see cref="ILocalAccountStore"/> over the shared
/// <see cref="OpenIdDbContext"/>, reusing the framework store base for id/concurrency generation, normalization, and
/// resource-id addressing.
/// </summary>
internal sealed class LocalAccountStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithResourceId<PersistedLocalAccount, LocalAccountEntity>, ILocalAccountStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    private DbSet<LocalAccountEntity> Accounts => DbContext.Set<LocalAccountEntity>();

    /// <inheritdoc />
    protected override ValueTask<PersistedLocalAccount> MapFromEntityAsync(
        LocalAccountEntity entity,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(MapFromEntity(entity));

    /// <inheritdoc />
    protected override async ValueTask<LocalAccountEntity?> GetEntityOrDefaultAsync(
        Expression<Func<LocalAccountEntity, bool>> predicate,
        CancellationToken cancellationToken
    ) =>
        await Accounts
            .Include(entity => entity.Claims)
            .FirstOrDefaultAsync(predicate, cancellationToken);

    /// <inheritdoc />
    protected override async ValueTask<LocalAccountEntity?> GetEntityOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var normalizedLocalAccountId = Normalize(resourceId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedLocalAccountId == normalizedLocalAccountId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<LocalAccountEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = Accounts.Include(entity => entity.Claims).AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(LocalAccountEntity entity) => entity.Id;

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedLocalAccount account,
        CancellationToken cancellationToken
    )
    {
        account.ConcurrencyToken = NextConcurrencyToken();

        var entity = new LocalAccountEntity
        {
            Id = NextId(),
            TenantId = account.TenantId,
            NormalizedTenantId = Normalize(account.TenantId),
            LocalAccountId = account.LocalAccountId,
            NormalizedLocalAccountId = Normalize(account.LocalAccountId),
            UserName = account.UserName,
            NormalizedUserName = Normalize(account.UserName),
            Email = account.Email,
            NormalizedEmail = Normalize(account.Email),
            EmailVerified = account.EmailVerified,
            PasswordHash = account.PasswordHash,
            SecurityStamp = account.SecurityStamp,
            IsEnabled = account.IsEnabled,
            ProfileMetadataJson = account.ProfileMetadata.OrEmptyObject(),
            SystemMetadataJson = account.SystemMetadata.OrEmptyObject(),
            ConcurrencyToken = account.ConcurrencyToken,
        };

        foreach (var claim in account.Claims)
        {
            entity.Claims.Add(
                new LocalAccountClaimEntity
                {
                    Id = NextId(),
                    NormalizedTenantId = Normalize(account.TenantId),
                    Type = claim.Type,
                    Value = claim.Value,
                }
            );
        }

        await Accounts.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask UpdateAsync(
        PersistedLocalAccount account,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityAsync(account.LocalAccountId, cancellationToken);

        entity.UserName = account.UserName;
        entity.NormalizedUserName = Normalize(account.UserName);
        entity.Email = account.Email;
        entity.NormalizedEmail = Normalize(account.Email);
        entity.EmailVerified = account.EmailVerified;
        entity.PasswordHash = account.PasswordHash;
        entity.SecurityStamp = account.SecurityStamp;
        entity.IsEnabled = account.IsEnabled;
        entity.ProfileMetadataJson = account.ProfileMetadata.OrEmptyObject();
        entity.SystemMetadataJson = account.SystemMetadata.OrEmptyObject();
    }

    /// <inheritdoc />
    public ValueTask<PersistedLocalAccount?> GetByIdOrDefaultAsync(
        string localAccountId,
        CancellationToken cancellationToken
    ) => GetOrDefaultAsync(localAccountId, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> GetByUserNameOrDefaultAsync(
        string userName,
        CancellationToken cancellationToken
    )
    {
        var normalizedUserName = Normalize(userName);
        return await GetOrDefaultAsync(
            entity => entity.NormalizedUserName == normalizedUserName,
            cancellationToken
        );
    }

    private static PersistedLocalAccount MapFromEntity(LocalAccountEntity entity) =>
        new()
        {
            TenantId = entity.TenantId,
            LocalAccountId = entity.LocalAccountId,
            UserName = entity.UserName,
            Email = entity.Email,
            EmailVerified = entity.EmailVerified,
            PasswordHash = entity.PasswordHash,
            SecurityStamp = entity.SecurityStamp,
            IsEnabled = entity.IsEnabled,
            ProfileMetadata = entity.ProfileMetadataJson,
            SystemMetadata = entity.SystemMetadataJson,
            Claims = entity
                .Claims.Select(claim => new PersistedLocalAccountClaim
                {
                    Type = claim.Type,
                    Value = claim.Value,
                })
                .ToList(),
            ConcurrencyToken = entity.ConcurrencyToken,
        };
}
