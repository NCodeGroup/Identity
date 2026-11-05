// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Learns of a change to the root key of an owner after it is committed, so that replicas can be told to drop the
/// keys they hold for the owner.
/// </summary>
/// <remarks>
/// The owner root key manager calls every registered observer after its commit, on its own bounded cancellation token
/// rather than the caller's, and logs and swallows each failure: a failing observer never fails or undoes the change.
/// </remarks>
// See ADR-1411.
[PublicAPI]
public interface IOwnerRootKeyChangeObserver
{
    /// <summary>
    /// Called after the root key of an owner is set, changes state or is removed.
    /// </summary>
    /// <param name="owner">The owner whose root key changed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ChangedAsync(SecretOwner owner, CancellationToken cancellationToken);
}
