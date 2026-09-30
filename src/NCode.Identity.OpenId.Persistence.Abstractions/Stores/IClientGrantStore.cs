#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
/// Provides an abstraction for a store which persists <see cref="PersistedClientGrant"/> instances: a client's
/// authorization to a resource server with a set of granted scopes.
/// </summary>
[PublicAPI]
public interface IClientGrantStore : IStore<PersistedClientGrant>
{
    /// <summary>
    /// Gets a single page of a client's <see cref="PersistedClientGrant"/> instances ordered by their identifier,
    /// using keyset (cursor) pagination.
    /// </summary>
    /// <param name="clientId">The opaque identifier of the client whose grants are returned.</param>
    /// <param name="cursor">The opaque cursor returned by a previous call that fetches the next page, or <c>null</c>
    /// to fetch the first page.</param>
    /// <param name="limit">The maximum number of items to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page and the
    /// cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedClientGrant>> GetPageAsync(
        string clientId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedClientGrant"/> from the store for the specified client and resource
    /// server.
    /// </summary>
    /// <param name="clientId">The opaque identifier of the client.</param>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedClientGrant"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedClientGrant?> GetOrDefaultAsync(
        string clientId,
        string resourceServerId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a client's grant to a resource server from the store.
    /// </summary>
    /// <param name="clientId">The opaque identifier of the client.</param>
    /// <param name="resourceServerId">The opaque identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when a
    /// grant was removed; otherwise <c>false</c>.</returns>
    ValueTask<bool> RemoveAsync(
        string clientId,
        string resourceServerId,
        CancellationToken cancellationToken
    );
}
