// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// A core-slice store base that combines the core tenant/secret helpers (<see cref="CoreBaseStore{TItem,TEntity}"/>)
/// with resource-id addressing (<see cref="IStore{TItem}"/>).
/// </summary>
/// <typeparam name="TItem">The type of the persisted item, also known as a <c>Data Transfer Object</c> or <c>DTO</c>.</typeparam>
/// <typeparam name="TEntity">The type of the corresponding entity.</typeparam>
internal abstract class CoreBaseStoreWithResourceId<TItem, TEntity>
    : CoreBaseStore<TItem, TEntity>,
        IStore<TItem>
    where TItem : class
    where TEntity : class
{
    /// <inheritdoc />
    public abstract ValueTask AddAsync(TItem item, CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract ValueTask UpdateAsync(TItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to get an entity from the store with the provided identifier.
    /// </summary>
    /// <param name="resourceId">The identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the entity if found; otherwise <c>null</c>.</returns>
    protected abstract ValueTask<TEntity?> GetEntityOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets an entity from the store with the provided identifier.
    /// </summary>
    /// <param name="resourceId">The identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the entity if found; otherwise throws an <see cref="InvalidOperationException"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity is not found.</exception>
    protected virtual async ValueTask<TEntity> GetEntityAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityOrDefaultAsync(resourceId, cancellationToken);
        return entity
            ?? throw new InvalidOperationException($"An entity with '{resourceId}' was not found.");
    }

    /// <summary>
    /// Attempts to get a persisted item from the store with the specified identifier.
    /// </summary>
    /// <param name="resourceId">The identifier of the persisted item to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the persisted item if found; otherwise <c>null</c>.</returns>
    public async ValueTask<TItem?> GetOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityOrDefaultAsync(resourceId, cancellationToken);
        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }
}
