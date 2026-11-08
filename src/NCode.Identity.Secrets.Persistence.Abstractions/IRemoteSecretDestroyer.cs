// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

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
}
