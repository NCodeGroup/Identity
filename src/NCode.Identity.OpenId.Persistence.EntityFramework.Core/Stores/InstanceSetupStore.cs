// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IInstanceSetupStore"/> that uses Entity Framework Core for
/// persistence. The setup state is a singleton: its row carries the key
/// <see cref="PersistedInstanceSetup.SingletonKey"/>, which is the primary key of the table.
/// </summary>
/// <remarks>
/// This store does not save changes. The caller's store manager commits them, which is where a second row for the
/// instance is rejected by the primary key and where an update from a stale read is rejected by the concurrency token.
/// </remarks>
internal class InstanceSetupStore(IStoreProvider storeProvider, OpenIdDbContext openIdDbContext)
    : IInstanceSetupStore
{
    private IStoreProvider StoreProvider { get; } = storeProvider;

    private OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    public object? GetService(Type serviceType) => StoreProvider.GetService(serviceType);

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : IStore => StoreProvider.GetStore<TStore>();

    /// <inheritdoc />
    public async ValueTask<PersistedInstanceSetup?> GetOrDefaultAsync(
        CancellationToken cancellationToken
    )
    {
        var entity = await DbContext
            .InstanceSetups.AsNoTracking()
            .SingleOrDefaultAsync(
                entity => entity.Key == PersistedInstanceSetup.SingletonKey,
                cancellationToken
            );

        return entity is null ? null : MapFromEntity(entity);
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedInstanceSetup item,
        CancellationToken cancellationToken
    )
    {
        // Assign the row token up front so create can return it without a re-read; the interceptor leaves a
        // pre-seeded insert token intact (ADR-1012).
        var concurrencyToken = Guid.NewGuid().ToString("N");

        var entity = new InstanceSetupEntity
        {
            Key = PersistedInstanceSetup.SingletonKey,
            IsInitialized = item.IsInitialized,
            TokenHash = item.TokenHash,
            TokenIssuedAt = item.TokenIssuedAt?.ToUniversalTime(),
            TokenExpiresAt = item.TokenExpiresAt?.ToUniversalTime(),
            InitializedAt = item.InitializedAt?.ToUniversalTime(),
            InitializedByPrincipalId = item.InitializedByPrincipalId,
            ConcurrencyToken = concurrencyToken,
        };

        await DbContext.InstanceSetups.AddAsync(entity, cancellationToken);

        item.ConcurrencyToken = concurrencyToken;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the instance setup is not in the store.</exception>
    /// <exception cref="StoreConcurrencyException">
    /// Thrown when the instance setup changed since the item was read.
    /// </exception>
    public async ValueTask UpdateAsync(
        PersistedInstanceSetup item,
        CancellationToken cancellationToken
    )
    {
        var entity =
            await DbContext.InstanceSetups.SingleOrDefaultAsync(
                entity => entity.Key == PersistedInstanceSetup.SingletonKey,
                cancellationToken
            ) ?? throw new InvalidOperationException("The instance setup was not found.");

        if (
            !string.Equals(item.ConcurrencyToken, entity.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            throw new StoreConcurrencyException(
                "The instance setup has been modified by another process. Please reload and try again."
            );
        }

        entity.IsInitialized = item.IsInitialized;
        entity.TokenHash = item.TokenHash;
        entity.TokenIssuedAt = item.TokenIssuedAt?.ToUniversalTime();
        entity.TokenExpiresAt = item.TokenExpiresAt?.ToUniversalTime();
        entity.InitializedAt = item.InitializedAt?.ToUniversalTime();
        entity.InitializedByPrincipalId = item.InitializedByPrincipalId;

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-1012).
        DbContext.InstanceSetups.Update(entity);
    }

    private static PersistedInstanceSetup MapFromEntity(InstanceSetupEntity entity) =>
        new()
        {
            Key = entity.Key,
            IsInitialized = entity.IsInitialized,
            TokenHash = entity.TokenHash,
            TokenIssuedAt = entity.TokenIssuedAt,
            TokenExpiresAt = entity.TokenExpiresAt,
            InitializedAt = entity.InitializedAt,
            InitializedByPrincipalId = entity.InitializedByPrincipalId,
            ConcurrencyToken = entity.ConcurrencyToken,
        };
}
