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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedClient"/> instances.
/// </summary>
[PublicAPI]
public interface IClientStore : IStore<PersistedClient>
{
    /// <summary>
    /// Attempts to get a <see cref="PersistedClient"/> instance by using its natural key.
    /// </summary>
    /// <param name="tenantId">The tenant identifier for the <see cref="PersistedClient"/> instance.</param>
    /// <param name="clientId">The natural key of the <see cref="PersistedClient"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedClient"/> instance matching the specified <paramref name="clientId"/> if it exists.</returns>
    ValueTask<PersistedClient?> TryGetByClientIdAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing persisted client in the store.
    /// The redirect urls and secrets collection are not updated by this method.
    /// </summary>
    /// <param name="persistedClient">The persisted client to update in the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateAsync(
        PersistedClient persistedClient,
        CancellationToken cancellationToken);
}
