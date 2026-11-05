// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// The exception that is thrown when the master keys of an owner cannot be used because the owner's root key is
/// unavailable: it failed its last probe, or the keys it wraps could not be unwrapped.
/// </summary>
/// <remarks>
/// It deliberately does not derive from <see cref="System.Security.Cryptography.CryptographicException"/>: a failure to
/// reach or use the owner's key never means that a master key is wrong, so development pruning never deletes it.
/// </remarks>
// See ADR-1411.
[PublicAPI]
public sealed class OwnerRootKeyUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerRootKeyUnavailableException"/> class.
    /// </summary>
    /// <param name="owner">The owner whose root key is unavailable.</param>
    public OwnerRootKeyUnavailableException(SecretOwner owner)
        : this(owner, innerException: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerRootKeyUnavailableException"/> class.
    /// </summary>
    /// <param name="owner">The owner whose root key is unavailable.</param>
    /// <param name="innerException">The exception that is the cause of this exception, or <see langword="null"/>.</param>
    public OwnerRootKeyUnavailableException(SecretOwner owner, Exception? innerException)
        : base(
            $"The root key of owner '{owner}' is unavailable: its master keys cannot be unwrapped until the key is restored and validated again.",
            innerException
        )
    {
        Owner = owner;
    }

    /// <summary>
    /// Gets the owner whose root key is unavailable.
    /// </summary>
    public SecretOwner Owner { get; }
}
