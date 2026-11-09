// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction to read the references to remote keys that the persisted secrets of every owner hold. A
/// secret references a key when its encoding is <c>remote</c> and its encoded value is <c>{provider}:{keyId}</c>.
/// </summary>
// See ADR-1478.
[PublicAPI]
public interface IRemoteKeyReferenceStore : IStore
{
    /// <summary>
    /// Gets a page of the key identifiers that the stored remote secrets of every owner reference through the
    /// provider, ordered by the secret, using keyset (cursor) pagination. A secret references a key when its encoding
    /// is <c>remote</c> and its encoded value is <c>{provider}:{keyId}</c>.
    /// </summary>
    /// <param name="provider">The name of the remote key provider whose key references are read.</param>
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of key
    /// identifiers and the cursor for the next page.</returns>
    // See ADR-1478.
    ValueTask<PagedResult<string>> GetKeyIdPageAsync(
        string provider,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
