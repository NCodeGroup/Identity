// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Buffers.Text;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IMasterKeyStore"/> that uses Entity Framework Core for
/// persistence. The owner of a master key is matched without regard to case by lower-casing the owner type and by
/// keeping a normalized copy of the owner identifier.
/// </summary>
/// <remarks>
/// This store does not save changes. The caller's store manager commits them, which is where a duplicate version for
/// the same owner is rejected by the unique index.
/// </remarks>
// See ADR-1235.
internal class MasterKeyStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : IMasterKeyStore
{
    private IStoreProvider StoreProvider { get; } = storeProvider;

    private IIdGenerator<long> IdGenerator { get; } = idGenerator;

    private OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    public object? GetService(Type serviceType) => StoreProvider.GetService(serviceType);

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : IStore => StoreProvider.GetStore<TStore>();

    /// <inheritdoc />
    public async ValueTask AddAsync(PersistedMasterKey item, CancellationToken cancellationToken)
    {
        var concurrencyToken = Guid.NewGuid().ToString("N");

        var entity = new MasterKeyEntity
        {
            Id = IdGenerator.CreateId(),
            KeyId = item.KeyId,
            OwnerType = item.OwnerType.ToLowerInvariant(),
            OwnerId = item.OwnerId,
            NormalizedOwnerId = item.OwnerId.ToLowerInvariant(),
            Version = item.Version,
            CreatedWhen = item.CreatedWhen.ToUniversalTime(),
            RetiredWhen = item.RetiredWhen?.ToUniversalTime(),
            RootKeyProvider = item.RootKeyProvider,
            RootKeyId = item.RootKeyId,
            RootWrappedKey = item.RootWrappedKey,
            RecoveryKeyId = item.RecoveryKeyId,
            RecoveryWrappedKey = item.RecoveryWrappedKey,
            KeyCheck = item.KeyCheck,
            ConcurrencyToken = concurrencyToken,
        };

        await DbContext.MasterKeys.AddAsync(entity, cancellationToken);

        item.ConcurrencyToken = concurrencyToken;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the master key is not in the store.</exception>
    /// <exception cref="StoreConcurrencyException">
    /// Thrown when the master key was modified since the item was read.
    /// </exception>
    public async ValueTask UpdateAsync(PersistedMasterKey item, CancellationToken cancellationToken)
    {
        var entity =
            await DbContext
                .MasterKeys.Where(entity => entity.KeyId == item.KeyId)
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"A master key with identifier '{item.KeyId}' was not found."
            );

        if (
            !string.Equals(item.ConcurrencyToken, entity.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            throw new StoreConcurrencyException(
                $"The master key with identifier '{item.KeyId}' has been modified by another process. Please reload and try again."
            );
        }

        entity.RetiredWhen = item.RetiredWhen?.ToUniversalTime();
        entity.RootKeyProvider = item.RootKeyProvider;
        entity.RootKeyId = item.RootKeyId;
        entity.RootWrappedKey = item.RootWrappedKey;
        entity.RecoveryKeyId = item.RecoveryKeyId;
        entity.RecoveryWrappedKey = item.RecoveryWrappedKey;
        entity.ConcurrencyToken = Guid.NewGuid().ToString("N");
    }

    /// <inheritdoc />
    public async ValueTask<PersistedMasterKey?> FindActiveAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var ownerType = owner.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = owner.OwnerId.ToLowerInvariant();

        var entity = await DbContext
            .MasterKeys.AsNoTracking()
            .Where(entity =>
                entity.OwnerType == ownerType
                && entity.NormalizedOwnerId == normalizedOwnerId
                && entity.RetiredWhen == null
            )
            .OrderByDescending(entity => entity.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : MapToPersistedMasterKey(entity);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedMasterKey>> FindByKeyIdsAsync(
        SecretOwner owner,
        IReadOnlyCollection<Guid> keyIds,
        CancellationToken cancellationToken
    )
    {
        if (keyIds.Count == 0)
            return [];

        var ownerType = owner.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = owner.OwnerId.ToLowerInvariant();

        var entities = await DbContext
            .MasterKeys.AsNoTracking()
            .Where(entity =>
                entity.OwnerType == ownerType
                && entity.NormalizedOwnerId == normalizedOwnerId
                && keyIds.Contains(entity.KeyId)
            )
            .ToListAsync(cancellationToken);

        return entities.Select(MapToPersistedMasterKey).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<int> GetLatestVersionAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var ownerType = owner.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = owner.OwnerId.ToLowerInvariant();

        var latestVersion = await DbContext
            .MasterKeys.AsNoTracking()
            .Where(entity =>
                entity.OwnerType == ownerType && entity.NormalizedOwnerId == normalizedOwnerId
            )
            .MaxAsync(entity => (int?)entity.Version, cancellationToken);

        return latestVersion ?? 0;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedMasterKey>> ListAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var ownerType = owner.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = owner.OwnerId.ToLowerInvariant();

        var entities = await DbContext
            .MasterKeys.AsNoTracking()
            .Where(entity =>
                entity.OwnerType == ownerType && entity.NormalizedOwnerId == normalizedOwnerId
            )
            .OrderByDescending(entity => entity.Version)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToPersistedMasterKey).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedMasterKey>> GetPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    ) => await GetPageAsync(DbContext.MasterKeys.AsNoTracking(), page, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedMasterKey>> GetActivePageAsync(
        DateTimeOffset createdBefore,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var instant = createdBefore.ToUniversalTime();

        return await GetPageAsync(
            DbContext
                .MasterKeys.AsNoTracking()
                .Where(entity => entity.RetiredWhen == null && entity.CreatedWhen < instant),
            page,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedMasterKey>> ListRetiredAsync(
        CancellationToken cancellationToken
    )
    {
        var entities = await DbContext
            .MasterKeys.AsNoTracking()
            .Where(entity => entity.RetiredWhen != null)
            .OrderBy(entity => entity.OwnerType)
            .ThenBy(entity => entity.NormalizedOwnerId)
            .ThenByDescending(entity => entity.Version)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToPersistedMasterKey).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(
        PersistedMasterKey item,
        CancellationToken cancellationToken
    )
    {
        var entity = await DbContext
            .MasterKeys.Where(entity => entity.KeyId == item.KeyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (entity is null)
            return false;

        if (
            !string.Equals(item.ConcurrencyToken, entity.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            throw new StoreConcurrencyException(
                $"The master key with identifier '{item.KeyId}' has been modified by another process. Please reload and try again."
            );
        }

        DbContext.MasterKeys.Remove(entity);

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<int> RemoveAllAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var ownerType = owner.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = owner.OwnerId.ToLowerInvariant();

        var entities = await DbContext
            .MasterKeys.Where(entity =>
                entity.OwnerType == ownerType && entity.NormalizedOwnerId == normalizedOwnerId
            )
            .ToListAsync(cancellationToken);

        DbContext.MasterKeys.RemoveRange(entities);

        return entities.Count;
    }

    // Reads one keyset page of the rows, ordered by row identifier. A full page fetches one extra row to signal that
    // another page exists, and a malformed cursor starts at the beginning.
    private static async ValueTask<PagedResult<PersistedMasterKey>> GetPageAsync(
        IQueryable<MasterKeyEntity> query,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var afterId = DecodeCursor(page.Cursor);
        if (afterId is not null)
            query = query.Where(entity => entity.Id > afterId.Value);

        var entities = await query
            .OrderBy(entity => entity.Id)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = entities.Count > page.Limit;
        var count = hasMore ? page.Limit : entities.Count;

        return new PagedResult<PersistedMasterKey>
        {
            Items = entities.Take(count).Select(MapToPersistedMasterKey).ToList(),
            NextCursor = hasMore ? EncodeCursor(entities[count - 1].Id) : null,
        };
    }

    private static string EncodeCursor(long key)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, key);
        return Base64Url.EncodeToString(bytes);
    }

    // A malformed cursor is treated as the start of the collection rather than a hard error.
    private static long? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return null;

        Span<byte> bytes = stackalloc byte[sizeof(long)];
        try
        {
            if (
                !Base64Url.TryDecodeFromChars(cursor, bytes, out var written)
                || written != sizeof(long)
            )
                return null;
        }
        catch (FormatException)
        {
            return null;
        }

        return BinaryPrimitives.ReadInt64BigEndian(bytes);
    }

    /// <summary>
    /// Maps a <see cref="MasterKeyEntity"/> to its corresponding <see cref="PersistedMasterKey"/> DTO.
    /// </summary>
    /// <param name="entity">The <see cref="MasterKeyEntity"/> to map.</param>
    /// <returns>The newly mapped <see cref="PersistedMasterKey"/> DTO.</returns>
    private static PersistedMasterKey MapToPersistedMasterKey(MasterKeyEntity entity) =>
        new()
        {
            KeyId = entity.KeyId,
            OwnerType = entity.OwnerType,
            OwnerId = entity.OwnerId,
            Version = entity.Version,
            CreatedWhen = entity.CreatedWhen,
            RetiredWhen = entity.RetiredWhen,
            RootKeyProvider = entity.RootKeyProvider,
            RootKeyId = entity.RootKeyId,
            RootWrappedKey = entity.RootWrappedKey,
            RecoveryKeyId = entity.RecoveryKeyId,
            RecoveryWrappedKey = entity.RecoveryWrappedKey,
            KeyCheck = entity.KeyCheck,
            ConcurrencyToken = entity.ConcurrencyToken,
        };
}
