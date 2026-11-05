// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Rewraps persisted master keys under the configured root key provider and recovery public key. Each master key is
/// unwrapped by the root key provider it names, or from its recovery copy with the recovery private key, and wrapped
/// again; its material does not change, so no replica needs to reload anything.
/// </summary>
/// <remarks>
/// This is the engine of the offline <c>secret-protection</c> command. Each master key is rewrapped in its own unit of
/// work, a conflicting write is retried, and a repeated run is harmless.
/// </remarks>
// See ADR-1408.
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
