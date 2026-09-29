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
using NCode.Identity.Secrets.Persistence.DataContracts;
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

    /// <summary>
    /// Attempts to get a single <see cref="PersistedSecret"/> for an OpenId Server by its identifier.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenId Server.</param>
    /// <param name="secretId">The identifier of the secret to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedSecret"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedSecret?> GetSecretOrDefaultAsync(
        string serverId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Adds a new secret to an OpenId Server and bumps the server's secrets concurrency token so that any
    /// running server instance refreshes its secret collection.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenId Server.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddSecretAsync(
        string serverId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the metadata of an existing secret for an OpenId Server (key material is immutable) and bumps
    /// the server's secrets concurrency token so that any running server instance refreshes its secret collection.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenId Server.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> whose metadata is updated. Its
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is checked for optimistic concurrency.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSecretAsync(
        string serverId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a secret from an OpenId Server and bumps the server's secrets concurrency token so that any
    /// running server instance refreshes its secret collection.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenId Server.</param>
    /// <param name="secretId">The identifier of the secret to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if a secret was removed; otherwise <c>false</c> when no matching secret existed.</returns>
    ValueTask<bool> RemoveSecretAsync(
        string serverId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes an OpenId Server from the store. The removal is rejected while the server still has dependent
    /// child resources (any secrets); those dependents must be removed first.
    /// </summary>
    /// <param name="serverId">The identifier of the OpenId Server to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if the server was removed; otherwise <c>false</c> when no matching server existed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the server still has dependent child resources.</exception>
    ValueTask<bool> RemoveAsync(string serverId, CancellationToken cancellationToken);
}
