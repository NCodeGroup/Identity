// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

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
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page and the
    /// cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedClientGrant>> GetPageAsync(
        string clientId,
        PageRequest page,
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
