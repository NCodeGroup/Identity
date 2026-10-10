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
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedLocalAccount"/> instances: the server-owned
/// credential, profile, and status payloads behind self-issued connections. A local account is written together with
/// its one-to-one self-issued federated identity in a single unit of work.
/// </summary>
[PublicAPI]
public interface ILocalAccountStore : IStore
{
    /// <summary>
    /// Adds a new local account to the store.
    /// </summary>
    /// <param name="account">The <see cref="PersistedLocalAccount"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync(PersistedLocalAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing local account in the store.
    /// </summary>
    /// <param name="account">The <see cref="PersistedLocalAccount"/> to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateAsync(PersistedLocalAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to get a local account from the store by its opaque identifier (its self-issued subject).
    /// </summary>
    /// <param name="localAccountId">The opaque identifier of the local account.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedLocalAccount"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedLocalAccount?> GetByIdOrDefaultAsync(
        string localAccountId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a local account from the store by its username (login handle).
    /// </summary>
    /// <param name="userName">The username of the local account.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedLocalAccount"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedLocalAccount?> GetByUserNameOrDefaultAsync(
        string userName,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets a single page of local accounts ordered by their identifier, using keyset (cursor) pagination.
    /// </summary>
    /// <param name="cursor">The opaque cursor returned by a previous call that fetches the next page, or <c>null</c>
    /// to fetch the first page.</param>
    /// <param name="limit">The maximum number of items to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page and the
    /// cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedLocalAccount>> GetPageAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a local account from the store.
    /// </summary>
    /// <param name="localAccountId">The opaque identifier of the local account to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(string localAccountId, CancellationToken cancellationToken);
}
