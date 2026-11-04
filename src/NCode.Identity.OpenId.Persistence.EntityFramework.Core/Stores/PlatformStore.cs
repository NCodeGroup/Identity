// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
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

    /// <summary>
    /// Maps a <see cref="PlatformEntity"/> to a <see cref="PersistedPlatformSecrets"/> instance.
    /// </summary>
    /// <param name="platformEntity">The <see cref="PlatformEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedPlatformSecrets"/> instance.</returns>
    protected internal virtual ValueTask<PersistedPlatformSecrets> MapSecretsAsync(
        PlatformEntity platformEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedPlatformSecrets
            {
                PlatformId = platformEntity.PlatformId,
                ConcurrencyToken = platformEntity.SecretsConcurrencyToken,
                Value = MapToPersistedSecrets(platformEntity.Secrets),
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
            Secrets = await MapSecretsAsync(entity, cancellationToken),
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
            ?? await DbContext
                .Platforms.Include(server => server.Secrets)
                    .ThenInclude(serverSecret => serverSecret.Secret)
                .SingleOrDefaultAsync(predicate, cancellationToken);
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
        var query = DbContext
            .Platforms.Include(server => server.Secrets)
                .ThenInclude(serverSecret => serverSecret.Secret)
            .AsQueryable();

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
    public async ValueTask<PersistedPlatformSecrets?> GetSecretsOrDefaultAsync(
        string platformId,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetEntityOrDefaultAsync(platformId, cancellationToken);
        if (platformEntity is null)
        {
            return null;
        }

        return await MapSecretsAsync(platformEntity, cancellationToken);
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
        persistedPlatform.Secrets.ConcurrencyToken = NextConcurrencyToken();

        var secrets = new List<PlatformSecretEntity>(persistedPlatform.Secrets.Value.Count);

        var platformEntity = new PlatformEntity
        {
            Id = NextId(),
            PlatformId = persistedPlatform.PlatformId,
            NormalizedPlatformId = Normalize(persistedPlatform.PlatformId),
            ConcurrencyToken = persistedPlatform.ConcurrencyToken,
            SettingsConcurrencyToken = persistedPlatform.Settings.ConcurrencyToken,
            SecretsConcurrencyToken = persistedPlatform.Secrets.ConcurrencyToken,
            DisplayName = persistedPlatform.DisplayName,
            SettingsJson = persistedPlatform.Settings.Value,
            ReconciledSettingsFingerprint = persistedPlatform.ReconciledSettingsFingerprint,
            ReconciledSettingsJson = persistedPlatform.ReconciledSettings,
            Secrets = secrets,
        };

        foreach (var persistedSecret in persistedPlatform.Secrets.Value)
        {
            var secretEntity = MapToSecretEntity(persistedSecret);

            await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

            var serverSecretEntity = new PlatformSecretEntity
            {
                Id = NextId(),

                Server = platformEntity,
                PlatformId = platformEntity.Id,

                Secret = secretEntity,
                SecretId = secretEntity.Id,
            };

            secrets.Add(serverSecretEntity);

            await DbContext.PlatformSecrets.AddAsync(serverSecretEntity, cancellationToken);
        }

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

    /// <inheritdoc />
    public async ValueTask<PersistedSecret?> GetSecretOrDefaultAsync(
        string platformId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var normalizedServerId = Normalize(platformId);
        var normalizedSecretId = Normalize(secretId);

        var secretEntity = await DbContext
            .PlatformSecrets.Where(serverSecret =>
                serverSecret.Server.NormalizedPlatformId == normalizedServerId
                && serverSecret.Secret.NormalizedSecretId == normalizedSecretId
            )
            .Select(serverSecret => serverSecret.Secret)
            .SingleOrDefaultAsync(cancellationToken);

        return secretEntity is null ? null : MapToPersistedSecret(secretEntity);
    }

    /// <inheritdoc />
    public async ValueTask AddSecretAsync(
        string platformId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetEntityAsync(platformId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var alreadyExists = platformEntity.Secrets.Any(serverSecret =>
            serverSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' already exists for platform with PlatformId='{platformId}'."
            );
        }

        // A created secret cannot have a concurrency conflict, so assign its token up front and return it on
        // the DTO; the interceptor leaves a pre-seeded insert token intact (ADR-1012).
        persistedSecret.ConcurrencyToken = NextConcurrencyToken();
        var secretEntity = MapToSecretEntity(persistedSecret);
        await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

        var serverSecretEntity = new PlatformSecretEntity
        {
            Id = NextId(),
            Server = platformEntity,
            PlatformId = platformEntity.Id,
            Secret = secretEntity,
            SecretId = secretEntity.Id,
        };
        await DbContext.PlatformSecrets.AddAsync(serverSecretEntity, cancellationToken);

        // The server is tracked; mutating the token marks it Modified via change detection.
        platformEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask UpdateSecretAsync(
        string platformId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetEntityAsync(platformId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var serverSecretEntity = platformEntity.Secrets.SingleOrDefault(serverSecret =>
            serverSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (serverSecretEntity is null)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' was not found for platform with PlatformId='{platformId}'."
            );
        }

        var secretEntity = serverSecretEntity.Secret;
        if (
            !string.Equals(
                persistedSecret.ConcurrencyToken,
                secretEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new StoreConcurrencyException(
                $"The secret with SecretId='{persistedSecret.SecretId}' has been modified by another process. Please reload and try again."
            );
        }

        // Only metadata is mutable; key material is immutable once generated (see ADR-1011). The secret's
        // row-level ConcurrencyToken is regenerated by the interceptor on save (ADR-1012).
        secretEntity.Use = persistedSecret.Use;
        secretEntity.Algorithm = persistedSecret.Algorithm;
        secretEntity.ExpiresWhen = persistedSecret.ExpiresWhen.ToUniversalTime();

        platformEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveSecretAsync(
        string platformId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetEntityAsync(platformId, cancellationToken);

        var normalizedSecretId = Normalize(secretId);
        var serverSecretEntity = platformEntity.Secrets.SingleOrDefault(serverSecret =>
            serverSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (serverSecretEntity is null)
        {
            return false;
        }

        DbContext.PlatformSecrets.Remove(serverSecretEntity);
        DbContext.Secrets.Remove(serverSecretEntity.Secret);

        // The server is tracked; mutating the token marks it Modified via change detection.
        platformEntity.SecretsConcurrencyToken = NextConcurrencyToken();

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken)
    {
        var platformEntity = await GetEntityOrDefaultAsync(platformId, cancellationToken);
        if (platformEntity is null)
        {
            return false;
        }

        var hasSecrets = await DbContext.PlatformSecrets.AnyAsync(
            serverSecret => serverSecret.PlatformId == platformEntity.Id,
            cancellationToken
        );
        if (hasSecrets)
        {
            throw new InvalidOperationException(
                $"The platform with PlatformId='{platformId}' cannot be removed because it still has one or more secrets."
            );
        }

        DbContext.Platforms.Remove(platformEntity);

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasDependentsAsync(
        string platformId,
        CancellationToken cancellationToken
    )
    {
        var platformEntity = await GetEntityOrDefaultAsync(platformId, cancellationToken);
        if (platformEntity is null)
        {
            return false;
        }

        return await DbContext.PlatformSecrets.AnyAsync(
            serverSecret => serverSecret.PlatformId == platformEntity.Id,
            cancellationToken
        );
    }
}
