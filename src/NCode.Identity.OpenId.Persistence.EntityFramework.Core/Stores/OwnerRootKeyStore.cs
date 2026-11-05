// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IOwnerRootKeyStore"/> that uses Entity Framework Core for
/// persistence. The owner is matched without regard to case by lower-casing the owner type and by keeping a normalized
/// copy of the owner identifier.
/// </summary>
/// <remarks>
/// This store does not save changes. The caller's store manager commits them, which is where a second root key for the
/// same owner is rejected by the unique index.
/// </remarks>
// See ADR-1414.
internal class OwnerRootKeyStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : IOwnerRootKeyStore
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
    public async ValueTask AddAsync(PersistedOwnerRootKey item, CancellationToken cancellationToken)
    {
        var concurrencyToken = Guid.NewGuid().ToString("N");

        var entity = new OwnerRootKeyEntity
        {
            Id = IdGenerator.CreateId(),
            OwnerType = item.OwnerType.ToLowerInvariant(),
            OwnerId = item.OwnerId,
            NormalizedOwnerId = item.OwnerId.ToLowerInvariant(),
            KeyWrapperName = item.KeyWrapperName,
            KeyReference = item.KeyReference,
            State = item.State,
            CreatedWhen = item.CreatedWhen.ToUniversalTime(),
            StateChangedWhen = item.StateChangedWhen.ToUniversalTime(),
            ValidatedWhen = item.ValidatedWhen?.ToUniversalTime(),
            FailedWhen = item.FailedWhen?.ToUniversalTime(),
            Failure = item.Failure,
            ConcurrencyToken = concurrencyToken,
        };

        await DbContext.OwnerRootKeys.AddAsync(entity, cancellationToken);

        item.ConcurrencyToken = concurrencyToken;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the root key is not in the store.</exception>
    /// <exception cref="StoreConcurrencyException">
    /// Thrown when the root key changed since the item was read.
    /// </exception>
    public async ValueTask UpdateAsync(
        PersistedOwnerRootKey item,
        CancellationToken cancellationToken
    )
    {
        var ownerType = item.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = item.OwnerId.ToLowerInvariant();

        var entity =
            await DbContext
                .OwnerRootKeys.Where(entity =>
                    entity.OwnerType == ownerType && entity.NormalizedOwnerId == normalizedOwnerId
                )
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"A root key for owner '{item.Owner}' was not found."
            );

        if (
            !string.Equals(item.ConcurrencyToken, entity.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            throw new StoreConcurrencyException(
                $"The root key of owner '{item.Owner}' has been modified by another process. Please reload and try again."
            );
        }

        entity.KeyWrapperName = item.KeyWrapperName;
        entity.KeyReference = item.KeyReference;
        entity.State = item.State;
        entity.CreatedWhen = item.CreatedWhen.ToUniversalTime();
        entity.StateChangedWhen = item.StateChangedWhen.ToUniversalTime();
        entity.ValidatedWhen = item.ValidatedWhen?.ToUniversalTime();
        entity.FailedWhen = item.FailedWhen?.ToUniversalTime();
        entity.Failure = item.Failure;

        entity.ConcurrencyToken = Guid.NewGuid().ToString("N");
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOwnerRootKey?> FindAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var ownerType = owner.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = owner.OwnerId.ToLowerInvariant();

        var entity = await DbContext
            .OwnerRootKeys.AsNoTracking()
            .Where(entity =>
                entity.OwnerType == ownerType && entity.NormalizedOwnerId == normalizedOwnerId
            )
            .SingleOrDefaultAsync(cancellationToken);

        return entity is null ? null : MapToPersistedOwnerRootKey(entity);
    }

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedOwnerRootKey>> GetDuePageAsync(
        DateTimeOffset validatedBefore,
        DateTimeOffset retryBefore,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var validated = validatedBefore.ToUniversalTime();
        var retry = retryBefore.ToUniversalTime();
        const string active = OwnerRootKeyStates.Active;

        // A state that is not active is retried on the failure clock, so a state that the code does not know is
        // retried like an unavailable key.
        var query = DbContext
            .OwnerRootKeys.AsNoTracking()
            .Where(entity =>
                (
                    entity.State == active
                    && (entity.ValidatedWhen == null || entity.ValidatedWhen < validated)
                )
                || (
                    entity.State != active
                    && (entity.FailedWhen == null || entity.FailedWhen < retry)
                )
            );

        var afterId = KeysetCursor.Decode(page.Cursor);
        if (afterId is not null)
            query = query.Where(entity => entity.Id > afterId.Value);

        var entities = await query
            .OrderBy(entity => entity.Id)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = entities.Count > page.Limit;
        var count = hasMore ? page.Limit : entities.Count;

        return new PagedResult<PersistedOwnerRootKey>
        {
            Items = entities.Take(count).Select(MapToPersistedOwnerRootKey).ToList(),
            NextCursor = hasMore ? KeysetCursor.Encode(entities[count - 1].Id) : null,
        };
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(
        PersistedOwnerRootKey item,
        CancellationToken cancellationToken
    )
    {
        var ownerType = item.OwnerType.ToLowerInvariant();
        var normalizedOwnerId = item.OwnerId.ToLowerInvariant();

        var entity = await DbContext
            .OwnerRootKeys.Where(entity =>
                entity.OwnerType == ownerType && entity.NormalizedOwnerId == normalizedOwnerId
            )
            .SingleOrDefaultAsync(cancellationToken);

        if (entity is null)
            return false;

        if (
            !string.Equals(item.ConcurrencyToken, entity.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            throw new StoreConcurrencyException(
                $"The root key of owner '{item.Owner}' has been modified by another process. Please reload and try again."
            );
        }

        DbContext.OwnerRootKeys.Remove(entity);

        return true;
    }

    private static PersistedOwnerRootKey MapToPersistedOwnerRootKey(OwnerRootKeyEntity entity) =>
        new()
        {
            OwnerType = entity.OwnerType,
            OwnerId = entity.OwnerId,
            KeyWrapperName = entity.KeyWrapperName,
            KeyReference = entity.KeyReference,
            State = entity.State,
            CreatedWhen = entity.CreatedWhen,
            StateChangedWhen = entity.StateChangedWhen,
            ValidatedWhen = entity.ValidatedWhen,
            FailedWhen = entity.FailedWhen,
            Failure = entity.Failure,
            ConcurrencyToken = entity.ConcurrencyToken,
        };
}
