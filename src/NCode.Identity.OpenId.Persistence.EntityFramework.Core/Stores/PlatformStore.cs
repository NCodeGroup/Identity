// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IPlatformStore"/> that uses Entity Framework Core for persistence.
/// </summary>
internal class PlatformStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : CoreBaseStoreWithResourceId<PersistedPlatform, PlatformEntity>, IPlatformStore
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
                PlatformId = platformEntity.PlatformId,
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
            PlatformId = entity.PlatformId,
            ConcurrencyToken = entity.ConcurrencyToken,
            DisplayName = entity.DisplayName,
            Settings = await MapSettingsAsync(entity, cancellationToken),
            ReconciledSettingsFingerprint = entity.ReconciledSettingsFingerprint,
            ReconciledSettings = entity.ReconciledSettingsJson,
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

    /// <inheritdoc />
    protected override async ValueTask<PlatformEntity?> GetEntityOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var normalizedServerId = Normalize(resourceId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedPlatformId == normalizedServerId,
            cancellationToken
        );
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
            query = query.Where(server => server.Id > id);
        }

        return await query.OrderBy(server => server.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(PlatformEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask<PersistedPlatformSettings?> GetSettingsOrDefaultAsync(
        string platformId,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetEntityOrDefaultAsync(platformId, cancellationToken);
        if (platformEntity is null)
        {
            return null;
        }

        return await MapSettingsAsync(platformEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
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
            PlatformId = persistedPlatform.PlatformId,
            NormalizedPlatformId = Normalize(persistedPlatform.PlatformId),
            ConcurrencyToken = persistedPlatform.ConcurrencyToken,
            SettingsConcurrencyToken = persistedPlatform.Settings.ConcurrencyToken,
            DisplayName = persistedPlatform.DisplayName,
            SettingsJson = persistedPlatform.Settings.Value,
            ReconciledSettingsFingerprint = persistedPlatform.ReconciledSettingsFingerprint,
            ReconciledSettingsJson = persistedPlatform.ReconciledSettings,
        };

        await DbContext.Platforms.AddAsync(platformEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask UpdateAsync(
        PersistedPlatform persistedPlatform,
        CancellationToken cancellationToken
    )
    {
        var platformId = persistedPlatform.PlatformId;
        var platformEntity = await GetEntityAsync(platformId, cancellationToken);

        if (
            !string.Equals(
                persistedPlatform.ConcurrencyToken,
                platformEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                $"The platform with PlatformId='{platformId}' has been modified by another process. Please reload and try again."
            );
        }

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-1012).
        platformEntity.DisplayName = persistedPlatform.DisplayName;

        DbContext.Platforms.Update(platformEntity);
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedPlatformSettings persistedServerSettings,
        CancellationToken cancellationToken
    )
    {
        var platformId = persistedServerSettings.PlatformId;
        var platformEntity = await GetEntityAsync(platformId, cancellationToken);

        if (
            !string.Equals(
                persistedServerSettings.ConcurrencyToken,
                platformEntity.SettingsConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                $"The settings for platform with PlatformId='{platformId}' have been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        platformEntity.SettingsConcurrencyToken = nextConcurrencyToken;
        platformEntity.SettingsJson = persistedServerSettings.Value;

        // A second change before the first is reconciled leaves settings that no pass reconciled, so the next pass has
        // nothing to compare with and reaches every tenant. See ADR-1234.
        if (platformEntity.ReconciledSettingsFingerprint is null)
            platformEntity.ReconciledSettingsJson = null;

        platformEntity.ReconciledSettingsFingerprint = null;

        DbContext.Platforms.Update(platformEntity);

        persistedServerSettings.ConcurrencyToken = nextConcurrencyToken;
    }

    /// <inheritdoc />
    public async ValueTask UpdateReconciledSettingsAsync(
        PersistedPlatform persistedPlatform,
        CancellationToken cancellationToken
    )
    {
        var platformId = persistedPlatform.PlatformId;
        var platformEntity = await GetEntityAsync(platformId, cancellationToken);

        if (
            !string.Equals(
                persistedPlatform.ConcurrencyToken,
                platformEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                $"The platform with PlatformId='{platformId}' has been modified by another process. Please reload and try again."
            );
        }

        // Set only these two columns and do not call Update, so that only they are written. The interceptor regenerates
        // the row ConcurrencyToken on save (ADR-1012).
        platformEntity.ReconciledSettingsFingerprint =
            persistedPlatform.ReconciledSettingsFingerprint;
        platformEntity.ReconciledSettingsJson = persistedPlatform.ReconciledSettings;
    }
}
