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

namespace NCode.Identity.OpenId.Stores;

/// <summary>
/// Provides an abstraction for the unit-of-work pattern.
/// </summary>
public interface IStoreManager : IServiceProvider, IAsyncDisposable
{
    /// <summary>
    /// Gets an <see cref="IStore{T}"/> instance of the specified type that can be used to query a database using the unit-of-work pattern.
    /// </summary>
    /// <typeparam name="TStore">The type of the store.</typeparam>
    TStore GetStore<TStore>()
        where TStore : class;

    /// <summary>
    /// Saves all changes made in this unit-of-work instance to the database.
    /// Relational databases may use transactions to ensure the consistency of the persisted data.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SaveChangesAsync(CancellationToken cancellationToken);
}
