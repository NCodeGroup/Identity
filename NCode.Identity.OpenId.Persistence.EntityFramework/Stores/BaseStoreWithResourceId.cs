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

using JetBrains.Annotations;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a base implementation for <see cref="IStore"/> that uses entity framework.
/// </summary>
/// <typeparam name="TItem">The type of the persisted item, also known as a <c>Data Transfer Object</c> or <c>DTO</c>.</typeparam>
/// <typeparam name="TEntity">The type of the corresponding entity.</typeparam>
[PublicAPI]
public abstract class BaseStoreWithResourceId<TItem, TEntity> : BaseStore<TItem, TEntity>, IStore<TItem>
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
        return entity ?? throw new InvalidOperationException($"An entity with '{resourceId}' was not found.");
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
}
