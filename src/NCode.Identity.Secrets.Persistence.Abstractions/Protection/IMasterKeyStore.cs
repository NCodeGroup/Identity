// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedMasterKey"/> instances.
/// </summary>
/// <remarks>
/// A master key is not scoped to a tenant because the master key of the platform has no tenant, so every query
/// identifies the owner explicitly.
/// </remarks>
// See ADR-1235.
[PublicAPI]
public interface IMasterKeyStore : IStore<PersistedMasterKey>
{
    /// <summary>
    /// Gets the active <see cref="PersistedMasterKey"/> instance of the specified owner, which is the master key that
    /// has not been retired.
    /// </summary>
    /// <remarks>
    /// The owner is matched without regard to case. An owner has at most one active master key. Should the store
    /// nonetheless hold several, the one with the highest version is returned. This query, together with
    /// <see cref="FindByKeyIdsAsync"/> and <see cref="GetLatestVersionAsync"/>, serves the read path so that only the
    /// rows that are needed are loaded.
    /// </remarks>
    /// <param name="owner">The <see cref="SecretOwner"/> of the <see cref="PersistedMasterKey"/> to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the active
    /// <see cref="PersistedMasterKey"/> of the owner, or <see langword="null"/> when the owner has none.</returns>
    ValueTask<PersistedMasterKey?> FindActiveAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the <see cref="PersistedMasterKey"/> instances of the specified owner whose identifiers are in the
    /// specified set, whether the master keys are active or retired.
    /// </summary>
    /// <remarks>
    /// The owner is matched without regard to case. A master key of another owner is never returned, even when its
    /// identifier is in the set. An identifier that does not name a master key of the owner is ignored, so the result
    /// may be smaller than the set. No ordering is guaranteed.
    /// </remarks>
    /// <param name="owner">The <see cref="SecretOwner"/> of the <see cref="PersistedMasterKey"/> instances to retrieve.</param>
    /// <param name="keyIds">The identifiers of the <see cref="PersistedMasterKey"/> instances to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the matching
    /// <see cref="PersistedMasterKey"/> instances, or an empty list when none match or the set is empty.</returns>
    ValueTask<IReadOnlyList<PersistedMasterKey>> FindByKeyIdsAsync(
        SecretOwner owner,
        IReadOnlyCollection<Guid> keyIds,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the highest version number among the <see cref="PersistedMasterKey"/> instances of the specified owner,
    /// counting both the active master key and the retired master keys.
    /// </summary>
    /// <remarks>
    /// The owner is matched without regard to case.
    /// </remarks>
    /// <param name="owner">The <see cref="SecretOwner"/> whose latest version to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the highest version
    /// number, or <c>0</c> when the owner has no master keys.</returns>
    ValueTask<int> GetLatestVersionAsync(SecretOwner owner, CancellationToken cancellationToken);

    /// <summary>
    /// Gets every <see cref="PersistedMasterKey"/> instance of the specified owner, both the active master key and the
    /// retired master keys, ordered with the newest version first.
    /// </summary>
    /// <remarks>
    /// The owner is matched without regard to case. This query returns every master key of the owner, so it is meant
    /// for administration. The read path loads only the rows that it needs through
    /// <see cref="FindActiveAsync"/> and <see cref="FindByKeyIdsAsync"/>.
    /// </remarks>
    /// <param name="owner">The <see cref="SecretOwner"/> of the <see cref="PersistedMasterKey"/> instances to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedMasterKey"/> instances of the owner, or an empty list when the owner has none.</returns>
    ValueTask<IReadOnlyList<PersistedMasterKey>> ListAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes every <see cref="PersistedMasterKey"/> instance of the specified owner from the store.
    /// </summary>
    /// <remarks>
    /// The owner is matched without regard to case. The values that the removed master keys protected can no longer
    /// be read once the removal is saved.
    /// </remarks>
    /// <param name="owner">The <see cref="SecretOwner"/> of the <see cref="PersistedMasterKey"/> instances to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of
    /// <see cref="PersistedMasterKey"/> instances that were removed.</returns>
    ValueTask<int> RemoveAllAsync(SecretOwner owner, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a page of every <see cref="PersistedMasterKey"/> instance of every owner, ordered by an opaque row
    /// identifier so that the pages are stable while rows are added or changed.
    /// </summary>
    /// <param name="page">The <see cref="PageRequest"/> that selects the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page.</returns>
    // See ADR-1407.
    ValueTask<PagedResult<PersistedMasterKey>> GetPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets a page of the active <see cref="PersistedMasterKey"/> instances that were created before an instant,
    /// ordered by an opaque row identifier.
    /// </summary>
    /// <param name="createdBefore">The instant before which an active master key must have been created.</param>
    /// <param name="page">The <see cref="PageRequest"/> that selects the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page.</returns>
    // See ADR-1407.
    ValueTask<PagedResult<PersistedMasterKey>> GetActivePageAsync(
        DateTimeOffset createdBefore,
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets a page of the owners that have retired master keys, read from the retired rows in an opaque row order.
    /// Only the owner of each row leaves the store, never its key material; an owner with several retired keys can
    /// appear on more than one page.
    /// </summary>
    /// <param name="page">The <see cref="PageRequest"/> that selects the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page, in which
    /// an owner appears once.</returns>
    // See ADR-1407.
    ValueTask<PagedResult<SecretOwner>> GetRetiredOwnerPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a <see cref="PersistedMasterKey"/> from the store, which destroys the key. The values that the key
    /// protected can no longer be read once the removal is saved.
    /// </summary>
    /// <param name="item">The <see cref="PersistedMasterKey"/> to remove, carrying the concurrency token it was read
    /// with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing
    /// <see langword="true"/> when the master key was removed, or <see langword="false"/> when it is not in the
    /// store.</returns>
    /// <exception cref="StoreConcurrencyException">
    /// Thrown when the master key was modified since the item was read.
    /// </exception>
    // See ADR-1407.
    ValueTask<bool> RemoveAsync(PersistedMasterKey item, CancellationToken cancellationToken);
}
