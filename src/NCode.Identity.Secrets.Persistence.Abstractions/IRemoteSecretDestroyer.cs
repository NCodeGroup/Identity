// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the ability to destroy, in its key management service, the key behind each removed secret that the server
/// created there. A caller runs it after the commit that removed the records, so a key is never destroyed while a
/// record still names it.
/// </summary>
// See ADR-1468 and ADR-1471.
[PublicAPI]
public interface IRemoteSecretDestroyer
{
    /// <summary>
    /// Destroys the key of every secret in <paramref name="removedSecrets"/> that is a remote secret the server
    /// generated, and skips every other secret. A failure to destroy one key is logged and does not stop the others,
    /// and the method never throws, since the removal is already committed.
    /// </summary>
    /// <param name="removedSecrets">The secrets whose records were removed.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask DestroyAsync(IReadOnlyCollection<PersistedSecret> removedSecrets);

    /// <summary>
    /// Destroys one key the server created, by its reference, on a bounded token of its own; returns
    /// <see langword="true"/> when the provider destroyed it and <see langword="false"/> when the provider is not
    /// registered or the call failed, which is logged. It never throws.
    /// </summary>
    /// <param name="reference">The reference to the key to destroy.</param>
    /// <returns>
    /// The <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, whose result is
    /// <see langword="true"/> when the provider destroyed the key.
    /// </returns>
    // See ADR-1478.
    ValueTask<bool> DestroyKeyAsync(RemoteKeyReference reference);
}
