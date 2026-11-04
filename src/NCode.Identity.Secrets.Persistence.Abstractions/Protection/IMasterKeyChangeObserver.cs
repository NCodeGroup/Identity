// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Learns of the changes to master keys after they are committed, so that replicas can be told and work can follow.
/// </summary>
/// <remarks>
/// The manager calls every registered observer after its commit, on its own bounded cancellation token rather than the
/// caller's, and logs and swallows each failure: a failing observer never fails or undoes the change.
/// </remarks>
// See ADR-1407.
[PublicAPI]
public interface IMasterKeyChangeObserver
{
    /// <summary>
    /// Called after an owner's master key rotated.
    /// </summary>
    /// <param name="rotation">The rotation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RotatedAsync(MasterKeyRotation rotation, CancellationToken cancellationToken);

    /// <summary>
    /// Called after a retired master key was destroyed.
    /// </summary>
    /// <param name="destroyed">The master key that was destroyed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask DestroyedAsync(MasterKeyInfo destroyed, CancellationToken cancellationToken);
}
