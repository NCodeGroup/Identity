// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedOwnerRootKey"/> instances.
/// </summary>
/// <remarks>
/// An owner root key is not scoped to a tenant, so every query identifies the owner explicitly.
/// <see cref="IStore{T}.UpdateAsync"/> throws a <see cref="StoreConcurrencyException"/> when the concurrency token
/// does not match, and an <see cref="InvalidOperationException"/> when the row is gone.
/// </remarks>
// See ADR-1414.
[PublicAPI]
public interface IOwnerRootKeyStore : IStore<PersistedOwnerRootKey>
{
    /// <summary>
    /// Gets the <see cref="PersistedOwnerRootKey"/> of the specified owner. The owner is matched without regard to
    /// case.
    /// </summary>
    /// <param name="owner">The <see cref="SecretOwner"/> whose root key to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the root key, or
    /// <see langword="null"/> when the owner has none.</returns>
    ValueTask<PersistedOwnerRootKey?> FindAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets a page of the owner root keys that are due for a probe, ordered by an opaque row identifier. A key is due
    /// when it is <see cref="OwnerRootKeyStates.Active"/> and was last validated before <paramref name="validatedBefore"/>
    /// or never, or when it is <see cref="OwnerRootKeyStates.Unavailable"/> or <see cref="OwnerRootKeyStates.Removing"/>
    /// and last failed before <paramref name="retryBefore"/> or never.
    /// </summary>
    /// <param name="validatedBefore">The instant before which an active key's last validation makes it due.</param>
    /// <param name="retryBefore">The instant before which an unavailable or removing key's last failure makes it due.</param>
    /// <param name="page">The <see cref="PageRequest"/> that selects the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page.</returns>
    ValueTask<PagedResult<PersistedOwnerRootKey>> GetDuePageAsync(
        DateTimeOffset validatedBefore,
        DateTimeOffset retryBefore,
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a <see cref="PersistedOwnerRootKey"/> from the store.
    /// </summary>
    /// <param name="item">The <see cref="PersistedOwnerRootKey"/> to remove, carrying the concurrency token it was
    /// read with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing
    /// <see langword="true"/> when the root key is removed, or <see langword="false"/> when it is not in the store.</returns>
    /// <exception cref="StoreConcurrencyException">
    /// Thrown when the root key changed since the item was read.
    /// </exception>
    ValueTask<bool> RemoveAsync(PersistedOwnerRootKey item, CancellationToken cancellationToken);
}
