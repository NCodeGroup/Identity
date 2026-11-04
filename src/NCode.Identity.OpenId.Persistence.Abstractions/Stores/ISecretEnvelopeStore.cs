// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction to read and rewrite the protected value of the persisted secrets of an owner. Key material
/// is immutable, so the only member that changes is the envelope that protects the same material, which a rewrap
/// replaces with an envelope under another master key.
/// </summary>
// See ADR-1407.
[PublicAPI]
public interface ISecretEnvelopeStore : IStore
{
    /// <summary>
    /// Gets a single page of the protected values of the secrets that an owner holds, ordered by their identifier,
    /// using keyset (cursor) pagination. A tenant owner holds the secrets of the tenant, of its clients and of its
    /// resource servers; a server owner holds the secrets of the server; any other owner type holds none.
    /// </summary>
    /// <param name="owner">The <see cref="SecretOwner"/> whose secrets are read.</param>
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page and the
    /// cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedProtectedValue>> GetPageAsync(
        SecretOwner owner,
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Replaces the protected value of a secret and marks the secrets of the record that owns it as changed, so every
    /// replica reloads them.
    /// </summary>
    /// <param name="value">The value to write, carrying the concurrency token it was read with.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    /// <exception cref="StoreConcurrencyException">Thrown when the secret was modified since the value was read.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the secret does not exist.</exception>
    ValueTask UpdateAsync(PersistedProtectedValue value, CancellationToken cancellationToken);
}
