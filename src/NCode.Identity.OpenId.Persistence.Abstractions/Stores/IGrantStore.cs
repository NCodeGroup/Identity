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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for persisting any type of grant and their payloads to storage.
/// </summary>
[PublicAPI]
public interface IGrantStore : IStore<PersistedGrant>
{
    /// <summary>
    /// Gets a single page of <see cref="PersistedGrant"/> instances ordered by their identifier, using keyset
    /// (cursor) pagination, selected by the supplied <see cref="GrantFilter"/>.
    /// </summary>
    /// <param name="filter">The criteria selecting which grants are returned; an empty filter returns all grants.</param>
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of
    /// <see cref="PersistedGrant"/> instances and the cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedGrant>> GetPageAsync(
        GrantFilter filter,
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Soft-revokes every active grant selected by the supplied <see cref="GrantFilter"/> by setting its
    /// <c>RevokedWhen</c> timestamp; already-revoked grants are left unchanged. The caller should ensure the filter is
    /// not empty (see <see cref="GrantFilter.IsEmpty"/>) so an unscoped bulk revoke is not performed by accident.
    /// </summary>
    /// <param name="filter">The criteria selecting which grants are revoked.</param>
    /// <param name="revokedWhen">The timestamp to record as the revocation time.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of
    /// grants that were revoked.</returns>
    ValueTask<long> RevokeWhereAsync(
        GrantFilter filter,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Soft-revokes every active grant belonging to the specified refresh-token rotation family by setting its
    /// <c>RevokedWhen</c> timestamp; already-revoked grants are left unchanged. Used by refresh-token reuse detection
    /// to revoke the whole family when a replayed token is presented.
    /// </summary>
    /// <param name="refreshTokenLineageId">The opaque identifier of the refresh-token rotation family to revoke.</param>
    /// <param name="revokedWhen">The timestamp to record as the revocation time.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of
    /// grants that were revoked.</returns>
    ValueTask<long> RevokeByRefreshTokenLineageAsync(
        string refreshTokenLineageId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets every grant of the refresh-token rotation family that is not revoked. The grants are read into the unit of
    /// work, so the caller can update them in it; a grant that expired is included.
    /// </summary>
    /// <param name="refreshTokenLineageId">The opaque identifier of the refresh-token rotation family.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the grants of the
    /// family that are not revoked.</returns>
    ValueTask<IReadOnlyList<PersistedGrant>> GetActiveByRefreshTokenLineageAsync(
        string refreshTokenLineageId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to retrieve a persisted grant from the store by its opaque <c>GrantId</c>.
    /// </summary>
    /// <param name="grantId">The opaque identifier of the grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedGrant"/> instance if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedGrant?> GetOrDefaultAsync(
        string grantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to retrieve a persisted grant from the store.
    /// </summary>
    /// <param name="grantType">The type of the grant.</param>
    /// <param name="hashedKey">The hashed key of the resource associated with the grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedGrant"/> instance if found.</returns>
    ValueTask<PersistedGrant?> GetOrDefaultAsync(
        string grantType,
        string hashedKey,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes, when the unit of work is saved, up to <paramref name="limit"/> grants that ended before
    /// <paramref name="endedBefore"/>. A grant ends when it expires or, if earlier, when it is revoked; a grant that
    /// neither expires nor is revoked never ends.
    /// </summary>
    /// <remarks>
    /// A caller prunes by repeating the removal, each time in a new unit of work, until fewer grants than the limit
    /// are removed. Two callers that remove the same grants conflict: the later commit fails as a lost concurrency
    /// race, and that caller reads the next batch.
    /// </remarks>
    /// <param name="endedBefore">The time before which a grant must have ended to be removed.</param>
    /// <param name="limit">The greatest number of grants to remove; it must be at least one.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of grants staged for removal.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than one.</exception>
    ValueTask<int> RemoveEndedBeforeAsync(
        DateTimeOffset endedBefore,
        int limit,
        CancellationToken cancellationToken
    );
}
