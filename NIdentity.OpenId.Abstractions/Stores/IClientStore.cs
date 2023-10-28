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

/// <summary>
/// Provides an abstraction for a store which persists <see cref="Client"/> instances.
/// </summary>
public interface IClientStore : IStore<Client>
{
    /// <summary>
    /// Attempts to get a <see cref="Client"/> instance by using its natural key.
    /// </summary>
    /// <param name="tenantId">The tenant identifier for the <see cref="Client"/> instance.</param>
    /// <param name="clientId">The natural key of the <see cref="Client"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="Client"/> instance matching the specified <paramref name="clientId"/> if it exists.</returns>
    ValueTask<Client?> TryGetByClientIdAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken);
}
