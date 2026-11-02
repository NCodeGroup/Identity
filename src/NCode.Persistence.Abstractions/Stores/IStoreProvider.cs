// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Persistence.Stores;

/// <summary>
/// Provides the ability to get an <see cref="IStore{T}"/> instance of the specified type.
/// </summary>
[PublicAPI]
public interface IStoreProvider : IServiceProvider
{
    /// <summary>
    /// Gets an <see cref="IStore{T}"/> instance of the specified type that can be used to query a database using the unit-of-work pattern.
    /// </summary>
    /// <typeparam name="TStore">The type of the store.</typeparam>
    TStore GetStore<TStore>()
        where TStore : IStore;
}
