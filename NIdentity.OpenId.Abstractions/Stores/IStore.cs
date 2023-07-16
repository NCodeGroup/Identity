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

using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Stores;

public interface IStore<T> : IStore<T, long>
    where T : ISupportId<long>
{
    // nothing
}

public interface IStore<T, in TKey>
    where T : ISupportId<TKey>
    where TKey : IEquatable<TKey>
{
    ValueTask AddAsync(T item, CancellationToken cancellationToken);

    ValueTask RemoveByIdAsync(TKey id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an entity by using its surrogate key.
    /// </summary>
    /// <param name="id">The surrogate key of the entity to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// entity matching the specified <paramref name="id"/> if it exists.</returns>
    ValueTask<T?> TryGetByIdAsync(TKey id, CancellationToken cancellationToken);
}
