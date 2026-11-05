// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// The exception that is thrown when a key that an owner names fails its probe: the key cannot wrap and unwrap a value.
/// </summary>
// See ADR-1411.
[PublicAPI]
public sealed class OwnerRootKeyProbeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerRootKeyProbeException"/> class.
    /// </summary>
    /// <param name="owner">The owner that named the key.</param>
    /// <param name="keyWrapperName">The name of the key wrapper.</param>
    /// <param name="keyReference">The reference to the key.</param>
    /// <param name="failure">The description of the failure, which carries no key material.</param>
    /// <param name="unavailableRootKey">The stored root key when the failure made it unavailable, otherwise
    /// <see langword="null"/>.</param>
    public OwnerRootKeyProbeException(
        SecretOwner owner,
        string keyWrapperName,
        string keyReference,
        string failure,
        OwnerRootKeyInfo? unavailableRootKey = null
    )
        : base(
            $"The key '{keyReference}' of key wrapper '{keyWrapperName}' could not wrap and unwrap a probe for owner '{owner}': {failure}"
        )
    {
        Owner = owner;
        KeyWrapperName = keyWrapperName;
        KeyReference = keyReference;
        Failure = failure;
        UnavailableRootKey = unavailableRootKey;
    }

    /// <summary>
    /// Gets the owner that named the key.
    /// </summary>
    public SecretOwner Owner { get; }

    /// <summary>
    /// Gets the name of the key wrapper.
    /// </summary>
    public string KeyWrapperName { get; }

    /// <summary>
    /// Gets the reference to the key.
    /// </summary>
    public string KeyReference { get; }

    /// <summary>
    /// Gets the description of the failure, which carries no key material.
    /// </summary>
    public string Failure { get; }

    /// <summary>
    /// Gets the stored root key when the failure of the key the owner already uses made it unavailable, which is a
    /// change the audit trail records, otherwise <see langword="null"/>.
    /// </summary>
    public OwnerRootKeyInfo? UnavailableRootKey { get; }
}
