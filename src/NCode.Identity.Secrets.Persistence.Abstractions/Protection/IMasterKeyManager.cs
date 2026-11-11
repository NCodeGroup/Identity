// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Manages the lifecycle of the master keys of an owner: reports them, rotates the active one to a new version,
/// rewraps the values that retired keys protect, and destroys retired keys that nothing names any more.
/// </summary>
/// <remarks>
/// A rotation retires the active key and creates the next version. The values that the retired key protects are rewrapped
/// by <see cref="RewrapAsync"/>, which asks every registered <see cref="IProtectedValueSource"/> once, and
/// <see cref="DestroyRetiredAsync"/> destroys a retired key only after its grace and only when no source reports a
/// value that still names it. Every registered <see cref="IMasterKeyChangeObserver"/> learns of a rotation and of a
/// destruction after it is committed.
/// </remarks>
// See ADR-1407.
[PublicAPI]
public interface IMasterKeyManager
{
    /// <summary>
    /// Gets the master keys of an owner, newest version first. The result carries no key material, and reading it
    /// creates no key.
    /// </summary>
    /// <param name="owner">The owner whose master keys are reported.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the status.</returns>
    ValueTask<MasterKeyStatus> GetStatusAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Creates the next version's key material through the root key provider, then retires the active master key of an
    /// owner and stores the next version as the active one. An owner without any key receives its first and reports no
    /// retired key.
    /// </summary>
    /// <param name="owner">The owner whose master key rotates.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the rotation.</returns>
    /// <exception cref="StoreConcurrencyException">
    /// Thrown when another rotation retired or replaced the active key first, or when the owner has retired keys and no
    /// active key.
    /// </exception>
    /// <exception cref="MasterKeyRotationTooSoonException">
    /// Thrown when the last rotation of the owner is younger than twice <see cref="Options.MasterKeyOptions.RefreshInterval"/>.
    /// </exception>
    ValueTask<MasterKeyRotation> RotateAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Rewraps, under the active master key, every value that a retired master key of the owner protects, in one pass
    /// per registered source over every retired key of the owner. The keys of the owner are read again first, and
    /// nothing is read when the owner has no retired key.
    /// </summary>
    /// <param name="owner">The owner whose values are rewrapped.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of values
    /// that were rewrapped and the number that failed and still name a retired key.</returns>
    ValueTask<ProtectedValueRewrapResult> RewrapAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Destroys the retired master keys of an owner whose grace has passed and that no value names. Nothing is destroyed
    /// when destruction is switched off in the options.
    /// </summary>
    /// <param name="owner">The owner whose retired keys are destroyed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the keys that were
    /// destroyed.</returns>
    ValueTask<IReadOnlyList<MasterKeyInfo>> DestroyRetiredAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );
}
