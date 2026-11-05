// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Rewraps persisted master keys under the root of their owner: the root key the owner brings, or else the configured
/// root key provider and recovery public key. Each master key is unwrapped by the root key provider it names, or from
/// its recovery copy with the recovery private key, and wrapped again; its material does not change, so no replica
/// needs to reload anything.
/// </summary>
/// <remarks>
/// Two callers use this engine: the offline <c>secret-protection</c> command, and the owner root key manager, which
/// conforms the master keys of an owner to its root key (<see cref="MasterKeyRewrapRequest.NonconformingOnly"/>). Each
/// master key is rewrapped in its own unit of work, a conflicting write is retried, and a repeated run is harmless.
/// A master key under an owner root key is never rewrapped under the deployment's root key or the recovery key, and it
/// never gains a recovery copy.
/// </remarks>
// See ADR-1408 and ADR-1411.
[PublicAPI]
public interface IMasterKeyRewrapper
{
    /// <summary>
    /// Rewraps the master keys that the request selects.
    /// </summary>
    /// <param name="request">The <see cref="MasterKeyRewrapRequest"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the counts.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown before any row is read when the request is recovery-only and no recovery public key is configured, or when
    /// the recovery private key is not a valid RSA key.
    /// </exception>
    ValueTask<MasterKeyRewrapResult> RewrapAsync(
        MasterKeyRewrapRequest request,
        CancellationToken cancellationToken
    );
}
