// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IPlatformStore"/> that uses Entity Framework Core for persistence.
/// The platform is a singleton: its row carries the key <see cref="OpenIdResourceIds.Platform"/>, which a unique index
/// holds to one row.
/// </summary>
internal class PlatformStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : CoreBaseStore<PersistedPlatform, PlatformEntity>, IPlatformStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <summary>
    /// Maps a <see cref="PlatformEntity"/> to a <see cref="PersistedPlatformSettings"/> instance.
    /// </summary>
    /// <param name="platformEntity">The <see cref="PlatformEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedPlatformSettings"/> instance.</returns>
    protected internal virtual ValueTask<PersistedPlatformSettings> MapSettingsAsync(
        PlatformEntity platformEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedPlatformSettings
            {
                ConcurrencyToken = platformEntity.SettingsConcurrencyToken,
                Value = platformEntity.SettingsJson,
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<PersistedPlatform> MapFromEntityAsync(
        PlatformEntity entity,
        CancellationToken cancellationToken
    )
    {
        return new PersistedPlatform
        {
            ConcurrencyToken = entity.ConcurrencyToken,
            Settings = await MapSettingsAsync(entity, cancellationToken),
        };
    }

    /// <inheritdoc />
    protected override async ValueTask<PlatformEntity?> GetEntityOrDefaultAsync(
        Expression<Func<PlatformEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext.Platforms.SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// Attempts to get the platform's entity from the store.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the entity if it exists.</returns>
    private async ValueTask<PlatformEntity?> GetPlatformEntityOrDefaultAsync(
        CancellationToken cancellationToken
    )
    {
        return await GetEntityOrDefaultAsync(
            entity => entity.Key == OpenIdResourceIds.Platform,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the platform's entity from the store.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the platform is not in the store.</exception>
    private async ValueTask<PlatformEntity> GetPlatformEntityAsync(
        CancellationToken cancellationToken
    )
    {
        return await GetPlatformEntityOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The platform was not found.");
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<PlatformEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext.Platforms.AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(platform => platform.Id > id);
        }

        return await query
            .OrderBy(platform => platform.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(PlatformEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask<PersistedPlatform?> GetOrDefaultAsync(
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetPlatformEntityOrDefaultAsync(cancellationToken);
        return platformEntity is null
            ? null
            : await MapFromEntityAsync(platformEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedPlatformSettings?> GetSettingsOrDefaultAsync(
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetPlatformEntityOrDefaultAsync(cancellationToken);
        if (platformEntity is null)
        {
            return null;
        }

        return await MapSettingsAsync(platformEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedPlatform persistedPlatform,
        CancellationToken cancellationToken
    )
    {
        // Assign the row token up front so create can return it without a re-read; the interceptor leaves a
        // pre-seeded insert token intact (ADR-1012). The sub-resource version columns stay manual.
        persistedPlatform.ConcurrencyToken = NextConcurrencyToken();
        persistedPlatform.Settings.ConcurrencyToken = NextConcurrencyToken();

        var platformEntity = new PlatformEntity
        {
            Id = NextId(),
            Key = OpenIdResourceIds.Platform,
            ConcurrencyToken = persistedPlatform.ConcurrencyToken,
            SettingsConcurrencyToken = persistedPlatform.Settings.ConcurrencyToken,
            SettingsJson = persistedPlatform.Settings.Value,
        };

        await DbContext.Platforms.AddAsync(platformEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        PersistedPlatform persistedPlatform,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetPlatformEntityAsync(cancellationToken);

        if (
            !string.Equals(
                persistedPlatform.ConcurrencyToken,
                platformEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                "The platform has been modified by another process. Please reload and try again."
            );
        }

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-1012).
        DbContext.Platforms.Update(platformEntity);
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedPlatformSettings persistedPlatformSettings,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetPlatformEntityAsync(cancellationToken);

        if (
            !string.Equals(
                persistedPlatformSettings.ConcurrencyToken,
                platformEntity.SettingsConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                "The platform's settings have been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        platformEntity.SettingsConcurrencyToken = nextConcurrencyToken;
        platformEntity.SettingsJson = persistedPlatformSettings.Value;

        DbContext.Platforms.Update(platformEntity);

        persistedPlatformSettings.ConcurrencyToken = nextConcurrencyToken;
    }
}
