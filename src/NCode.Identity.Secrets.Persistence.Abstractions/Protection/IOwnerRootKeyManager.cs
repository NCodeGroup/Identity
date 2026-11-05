// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Manages the root key that an owner brings: sets it after probing it, validates it again over time, and removes it.
/// While an owner has a root key, its master keys are wrapped under that key with no recovery copy.
/// </summary>
/// <remarks>
/// Which owners can bring a root key is decided by the registered <see cref="IOwnerRootKeyPolicy"/> implementations,
/// which govern <see cref="SetAsync"/> only: the master keys of the <c>server</c> owner, for example, are always wrapped
/// by the deployment's root key, so it cannot bring one. Setting or changing the key rotates the owner's master key
/// and rewraps the retired ones under the key; removing it moves the master keys back under the deployment's root key
/// without rotating. Every registered <see cref="IOwnerRootKeyChangeObserver"/> learns of a state change after it is
/// committed.
/// </remarks>
// See ADR-1414.
[PublicAPI]
public interface IOwnerRootKeyManager
{
    /// <summary>
    /// Gets the names of the key wrappers that an owner may choose for its root key, in ordinal order. The list is empty
    /// when the deployment lets no owner bring a key.
    /// </summary>
    IReadOnlyList<string> AllowedKeyWrapperNames { get; }

    /// <summary>
    /// Checks whether an owner can bring a root key, which every registered <see cref="IOwnerRootKeyPolicy"/> has to
    /// allow.
    /// </summary>
    /// <param name="owner">The owner to check.</param>
    /// <param name="reason">
    /// When this method returns <see langword="false"/>, the reason that the first refusing policy gave; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the owner can bring a root key; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the owner is the default value of <see cref="SecretOwner"/>.</exception>
    bool CanBringRootKey(SecretOwner owner, [NotNullWhen(false)] out string? reason);

    /// <summary>
    /// Checks that a key wrapper may be chosen and that it accepts the syntax of a key reference, which is at most
    /// <see cref="NCode.Identity.Secrets.Persistence.DataContracts.PersistedOwnerRootKey.MaxKeyReferenceLength"/>
    /// characters.
    /// </summary>
    /// <param name="keyWrapperName">The name of the key wrapper.</param>
    /// <param name="keyReference">The reference to the key.</param>
    /// <param name="failure">
    /// When this method returns <see langword="false"/>, a sentence that describes why the choice is refused;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the choice is acceptable; otherwise, <see langword="false"/>.</returns>
    bool TryValidateKeyReference(
        string keyWrapperName,
        string keyReference,
        [NotNullWhen(false)] out string? failure
    );

    /// <summary>
    /// Gets the root key of an owner. The result carries no key material.
    /// </summary>
    /// <param name="owner">The owner whose root key is reported.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the root key, or
    /// <see langword="null"/> when the owner has none.</returns>
    ValueTask<OwnerRootKeyInfo?> GetAsync(SecretOwner owner, CancellationToken cancellationToken);

    /// <summary>
    /// Sets or changes the root key of an owner. The key is probed first; the owner's master key rotates onto it and
    /// every retired master key is rewrapped under it.
    /// </summary>
    /// <param name="owner">The owner that brings the key.</param>
    /// <param name="keyWrapperName">The name of the key wrapper, which has to be allowed.</param>
    /// <param name="keyReference">The reference to the key.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the result.</returns>
    /// <exception cref="OwnerRootKeyNotAllowedException">
    /// Thrown before anything is probed or written when a registered <see cref="IOwnerRootKeyPolicy"/> refuses the
    /// owner.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the key wrapper is not allowed or rejects the reference, or the reference is too long.
    /// </exception>
    /// <exception cref="OwnerRootKeyProbeException">Thrown when the key fails its probe.</exception>
    /// <exception cref="MasterKeyRotationTooSoonException">
    /// Thrown before anything is written when the owner's master key rotated too recently to rotate onto a different
    /// key.
    /// </exception>
    /// <exception cref="StoreConcurrencyException">Thrown when another writer stored a root key first.</exception>
    ValueTask<OwnerRootKeyChange> SetAsync(
        SecretOwner owner,
        string keyWrapperName,
        string keyReference,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Starts removing the root key of an owner: new master keys are wrapped under the deployment's root key, and every
    /// master key moves under it. The record is deleted once no master key remains under the key.
    /// </summary>
    /// <param name="owner">The owner whose root key is removed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the result.</returns>
    ValueTask<OwnerRootKeyRemoval> RemoveAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Probes the root key of an owner and records the result: a failure makes the owner fail closed, and a success
    /// restores it. A key that is being removed completes its removal instead, and an active key completes any pending
    /// rotation and rewrap.
    /// </summary>
    /// <param name="owner">The owner whose root key is validated.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the result, whose
    /// root key is <see langword="null"/> when the owner has none.</returns>
    ValueTask<OwnerRootKeyValidation> ValidateAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    );
}
