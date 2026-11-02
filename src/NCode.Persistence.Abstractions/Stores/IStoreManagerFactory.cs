// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a factory that creates <see cref="IStoreManager"/> instances.
/// </summary>
[PublicAPI]
public interface IStoreManagerFactory
{
    /// <summary>
    /// Creates a new instance of the <see cref="IStoreManager"/> instance.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// newly created <see cref="IStoreManager"/> instance.</returns>
    ValueTask<IStoreManager> CreateAsync(CancellationToken cancellationToken);
}
