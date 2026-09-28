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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedServer"/> instances.
/// </summary>
[PublicAPI]
public interface IServerStore : IStore<PersistedServer>
{
    /// <summary>
    /// Attempts to get a <see cref="PersistedServer"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="serverId">The identifier of the <see cref="PersistedServer"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedServer"/> instance matching the specified entity if it exists.</returns>
    ValueTask<PersistedServer?> GetOrDefaultAsync(
        string serverId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the <see cref="PersistedServerSettings"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenId Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation. </param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedServerSettings"/> for the specified identifier.</returns>
    ValueTask<PersistedServerSettings?> GetSettingsOrDefaultAsync(
        string serverId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the <see cref="PersistedServerSecrets"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenId Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedServerSecrets"/> for the specified identifier.</returns>
    ValueTask<PersistedServerSecrets?> GetSecretsOrDefaultAsync(
        string serverId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the JSON settings for an OpenId Server in the store.
    /// </summary>
    /// <param name="persistedServerSettings">The <see cref="PersistedServerSettings"/> instance to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSettingsAsync(
        PersistedServerSettings persistedServerSettings,
        CancellationToken cancellationToken
    );
}
