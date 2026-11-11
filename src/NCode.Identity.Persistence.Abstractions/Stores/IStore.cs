// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Persistence.Stores;

/// <summary>
/// Base interface for all store implementations.
/// </summary>
[PublicAPI]
public interface IStore : IStoreProvider
{
    // nothing
}

/// <summary>
/// Provides an abstraction for a store which manages persisted items.
/// </summary>
/// <typeparam name="T">The type of the persisted item.</typeparam>
[PublicAPI]
public interface IStore<in T> : IStore
{
    /// <summary>
    /// Adds a new persisted item to the store.
    /// </summary>
    /// <param name="item">The item to add to the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync(T item, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a persisted item in the store.
    /// </summary>
    /// <param name="item">The item to update in the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateAsync(T item, CancellationToken cancellationToken);
}
