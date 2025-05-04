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
    /// Attempts to get a <see cref="PersistedClient"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="clientId">The identifier of the <see cref="PersistedClient"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedClient"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedClient?> GetOrDefaultAsync(
        string clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the JSON settings for an OpenId Client in the store.
    /// </summary>
    /// <param name="persistedClientSettings">The <see cref="PersistedClientSettings"/> instance to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSettingsAsync(
        PersistedClientSettings persistedClientSettings,
        CancellationToken cancellationToken
    );
}
