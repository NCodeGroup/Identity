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
/// Provides the single pluggable seam for a host's local accounts: the server-owned credential, profile, and status
/// payloads behind self-issued connections. It is intentionally backing-store-agnostic — the generic Entity Framework
/// reference implementation and the ASP.NET Core Identity adapter both implement it — so credential handling (which
/// differs by backing store) is expressed as first-class operations (<see cref="AddAsync"/>,
/// <see cref="SetPasswordAsync"/>, <see cref="VerifyCredentialAsync"/>) rather than by exposing a raw password hash on
/// the <see cref="PersistedLocalAccount"/> data contract. A host registers an implementation (or an opt-in reference
/// package supplies one) to enable the resource-owner password grant and the local-account management endpoints; when
/// none is registered, those surfaces report local accounts as unsupported.
/// </summary>
[PublicAPI]
public interface ILocalAccountStore : IStore
{
    /// <summary>
    /// Adds a new local account to the store, hashing the supplied credential. The store generates the account's
    /// security stamp.
    /// </summary>
    /// <param name="account">The <see cref="PersistedLocalAccount"/> to add.</param>
    /// <param name="password">The UTF-8 bytes of the account's initial password, carried in a caller-owned buffer so
    /// the sensitive material can be zeroed after use; or <c>null</c> for a passwordless account.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync(
        PersistedLocalAccount account,
        ReadOnlyMemory<byte>? password,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the mutable profile and status fields (username, email, email-verified, and enabled) of an existing
    /// local account. The credential is managed through <see cref="SetPasswordAsync"/> and the claims through
    /// <see cref="ReplaceClaimsAsync"/>.
    /// </summary>
    /// <param name="account">The <see cref="PersistedLocalAccount"/> to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateAsync(PersistedLocalAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the credential of an existing local account, hashing the supplied password and rotating the account's
    /// security stamp so that outstanding sessions and tokens issued before the reset are invalidated.
    /// </summary>
    /// <param name="localAccountId">The opaque identifier of the local account whose credential is reset.</param>
    /// <param name="password">The UTF-8 bytes of the account's new password, carried in a caller-owned buffer so the
    /// sensitive material can be zeroed after use.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetPasswordAsync(
        string localAccountId,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Verifies a username and password credential, returning the account when the credential is valid and the account
    /// is enabled. The store performs the hash comparison (and may transparently upgrade a stored hash to current
    /// parameters on success).
    /// </summary>
    /// <param name="userName">The username (login handle) presented by the end-user.</param>
    /// <param name="password">The UTF-8 bytes of the password presented by the end-user, carried in a caller-owned
    /// buffer so the sensitive material can be zeroed after use.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the authenticated
    /// <see cref="PersistedLocalAccount"/> when the credential is valid; otherwise <c>null</c>.</returns>
    ValueTask<PersistedLocalAccount?> VerifyCredentialAsync(
        string userName,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    );

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

    /// <summary>
    /// Replaces the full set of profile claims of an existing local account with the supplied claims, bumping the
    /// account's concurrency token.
    /// </summary>
    /// <param name="localAccountId">The opaque identifier of the local account whose claims are replaced.</param>
    /// <param name="claims">The replacement profile claims.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ReplaceClaimsAsync(
        string localAccountId,
        IReadOnlyList<PersistedLocalAccountClaim> claims,
        CancellationToken cancellationToken
    );
}
