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

using System.Linq.Expressions;
using JetBrains.Annotations;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a base implementation for <see cref="IStore{TItem}"/> that uses entity framework.
/// </summary>
/// <typeparam name="TItem">The type of the persisted item, also known as a <c>Data Transfer Object</c> or <c>DTO</c>.</typeparam>
/// <typeparam name="TEntity">The type of the corresponding entity.</typeparam>
[PublicAPI]
public abstract class BaseStoreWithEntityId<TItem, TEntity> : BaseStore<TItem, TEntity>, IStore<TItem>
    where TItem : class, ISupportId<long>
    where TEntity : class, ISupportId<long>
{
    /// <inheritdoc />
    public virtual bool IsRemoveSupported => false;

    /// <inheritdoc />
    public abstract ValueTask AddAsync(TItem item, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask RemoveByIdAsync(long id, CancellationToken cancellationToken)
    {
        if (!IsRemoveSupported)
            throw new NotSupportedException("Remove operation is not supported.");

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public virtual async ValueTask<TItem?> TryGetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await TryGetAsync(entity => entity.Id == id, cancellationToken);
    }

    //

    /// <summary>
    /// Attempts to retrieve an entity from the store based on the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to use to find the entity.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// entity if found; otherwise <c>null</c>.</returns>
    protected abstract ValueTask<TEntity?> TryGetEntityAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets an entity from the store based on its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// entity if found; otherwise throws an <see cref="InvalidOperationException"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity is not found.</exception>
    protected async ValueTask<TEntity> GetEntityByIdAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await TryGetEntityAsync(entity => entity.Id == id, cancellationToken);
        return entity ?? throw new InvalidOperationException($"Entity with ID {id} not found.");
    }

    /// <summary>
    /// Attempts to retrieve a DTO from the store based on the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to use to find the entity.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// DTO if found; otherwise <c>null</c>.</returns>
    protected virtual async ValueTask<TItem?> TryGetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        var entity = await TryGetEntityAsync(predicate, cancellationToken);
        return entity is null ? null : await MapAsync(entity, cancellationToken);
    }
}
