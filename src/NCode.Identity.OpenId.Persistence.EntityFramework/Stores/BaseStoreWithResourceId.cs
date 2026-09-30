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

using System.Buffers.Binary;
using System.Buffers.Text;
using JetBrains.Annotations;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a base implementation for <see cref="IStore"/> that uses entity framework.
/// </summary>
/// <typeparam name="TItem">The type of the persisted item, also known as a <c>Data Transfer Object</c> or <c>DTO</c>.</typeparam>
/// <typeparam name="TEntity">The type of the corresponding entity.</typeparam>
[PublicAPI]
internal abstract class BaseStoreWithResourceId<TItem, TEntity>
    : BaseStore<TItem, TEntity>,
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
    /// <param name="resourceId">The identifier of the persisted item  to retrieve.</param>
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

    /// <summary>
    /// Gets a single page of entities ordered by their surrogate identifier, starting after the specified value.
    /// </summary>
    /// <param name="afterId">The exclusive lower-bound surrogate identifier, or <c>null</c> to start at the beginning.</param>
    /// <param name="take">The maximum number of entities to return (the caller requests one extra to detect a next page).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of entities.</returns>
    protected abstract ValueTask<IReadOnlyList<TEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the keyset sort key for the specified entity (its surrogate identifier).
    /// </summary>
    /// <param name="entity">The entity whose sort key is returned.</param>
    /// <returns>The sort key.</returns>
    protected abstract long GetSortKey(TEntity entity);

    /// <summary>
    /// Gets a single page of persisted items using keyset (cursor) pagination.
    /// </summary>
    /// <param name="cursor">The opaque cursor returned by a previous call, or <c>null</c> to fetch the first page.</param>
    /// <param name="limit">The maximum number of items to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page and the
    /// cursor for the next page.</returns>
    public async ValueTask<PagedResult<TItem>> GetPageAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var afterId = DecodeCursor(cursor);

        // Fetch one extra row so a full page signals that another page exists.
        var entities = await GetEntityPageAsync(afterId, limit + 1, cancellationToken);

        var hasMore = entities.Count > limit;
        var count = hasMore ? limit : entities.Count;

        var items = new List<TItem>(count);
        for (var index = 0; index < count; index++)
        {
            items.Add(await MapFromEntityAsync(entities[index], cancellationToken));
        }

        var nextCursor = hasMore ? EncodeCursor(GetSortKey(entities[count - 1])) : null;

        return new PagedResult<TItem> { Items = items, NextCursor = nextCursor };
    }

    private static string EncodeCursor(long key)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, key);
        return Base64Url.EncodeToString(bytes);
    }

    private static long? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return null;

        // A malformed cursor is treated as the start of the collection rather than a hard error.
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        if (
            !Base64Url.TryDecodeFromChars(cursor, bytes, out var written)
            || written != sizeof(long)
        )
            return null;

        return BinaryPrimitives.ReadInt64BigEndian(bytes);
    }
}
