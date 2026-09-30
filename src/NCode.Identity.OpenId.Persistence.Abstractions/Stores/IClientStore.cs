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
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedClient"/> instances.
/// </summary>
[PublicAPI]
public interface IClientStore : IStore<PersistedClient>
{
    /// <summary>
    /// Gets a single page of <see cref="PersistedClient"/> instances ordered by their identifier, using keyset
    /// (cursor) pagination.
    /// </summary>
    /// <param name="cursor">The opaque cursor returned by a previous call that fetches the next page, or <c>null</c>
    /// to fetch the first page.</param>
    /// <param name="limit">The maximum number of items to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of
    /// <see cref="PersistedClient"/> instances and the cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedClient>> GetPageAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    );

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

    /// <summary>
    /// Attempts to get the <see cref="PersistedClientSecrets"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="clientId">The identifier of the OpenId Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedClientSecrets"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedClientSecrets?> GetSecretsOrDefaultAsync(
        string clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a single secret for an OpenId Client, resolved together with its owning tenant. The lookup
    /// queries the store directly instead of loading the whole secret collection.
    /// </summary>
    /// <param name="clientId">The identifier of the OpenId Client.</param>
    /// <param name="secretId">The identifier of the secret to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedClientSecret"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedClientSecret?> GetSecretOrDefaultAsync(
        string clientId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Adds a new secret to an OpenId Client and bumps the client's secrets concurrency token so that any
    /// running client instance refreshes its secret collection.
    /// </summary>
    /// <param name="clientId">The identifier of the OpenId Client.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddSecretAsync(
        string clientId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the metadata of an existing secret for an OpenId Client (key material is immutable) and bumps
    /// the client's secrets concurrency token so that any running client instance refreshes its secret collection.
    /// </summary>
    /// <param name="clientId">The identifier of the OpenId Client.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> whose metadata is updated. Its
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is checked for optimistic concurrency.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSecretAsync(
        string clientId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a secret from an OpenId Client and bumps the client's secrets concurrency token so that any
    /// running client instance refreshes its secret collection.
    /// </summary>
    /// <param name="clientId">The identifier of the OpenId Client.</param>
    /// <param name="secretId">The identifier of the secret to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if a secret was removed; otherwise <c>false</c> when no matching secret existed.</returns>
    ValueTask<bool> RemoveSecretAsync(
        string clientId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes an OpenId Client from the store. The removal is rejected while the client still has dependent
    /// child resources (any secrets); those dependents must be removed first.
    /// </summary>
    /// <param name="clientId">The identifier of the OpenId Client to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if the client was removed; otherwise <c>false</c> when no matching client existed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the client still has dependent child resources.</exception>
    ValueTask<bool> RemoveAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether an OpenId Client still has dependent child resources (any secrets) that would block
    /// its removal.
    /// </summary>
    /// <param name="clientId">The identifier of the OpenId Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// when the client has dependent child resources; otherwise <c>false</c> (including when no such client exists).</returns>
    ValueTask<bool> HasDependentsAsync(string clientId, CancellationToken cancellationToken);
}
