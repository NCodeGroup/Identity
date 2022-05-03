#region Copyright Preamble
//
//    Copyright @ 2021 NCode Group
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

using System.Threading;
using System.Threading.Tasks;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Stores;

/// <summary>
/// Provides an abstraction for a store which manages client applications.
/// </summary>
public interface IClientStore
{
    /// <summary>
    /// Gets a <see cref="Client"/> by using its surrogate key.
    /// </summary>
    /// <param name="id">The surrogate key of the <see cref="Client"/> to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="Client"/> matching the specified <paramref name="id"/> if it exists.</returns>
    ValueTask<Client?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a <see cref="Client"/> by using its natural key.
    /// </summary>
    /// <param name="clientId">The natural key of the <see cref="Client"/> to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="Client"/> matching the specified <paramref name="clientId"/> if it exists.</returns>
    ValueTask<Client?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken);
}