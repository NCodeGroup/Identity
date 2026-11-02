// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Persistence.Stores;

/// <summary>
/// Provides an abstraction for the unit-of-work pattern.
/// </summary>
[PublicAPI]
public interface IStoreManager : IStoreProvider, IAsyncDisposable
{
    /// <summary>
    /// Saves all changes made in this unit-of-work instance to the database.
    /// Relational databases may use transactions to ensure the consistency of the persisted data.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SaveChangesAsync(CancellationToken cancellationToken);
}
