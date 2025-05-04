#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
