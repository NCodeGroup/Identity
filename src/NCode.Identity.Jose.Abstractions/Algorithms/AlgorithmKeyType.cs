// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes one type of key that a <see cref="KeyedAlgorithm"/> accepts, together with the sizes it accepts keys of
/// that type in. An algorithm whose name serves several key types, such as <c>ECDH-ES</c> over <c>EC</c> and
/// <c>OKP</c> keys (RFC 8037 §3.2), lists one <see cref="AlgorithmKeyType"/> per key type.
/// </summary>
// See ADR-1389.
[PublicAPI]
public sealed class AlgorithmKeyType
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmKeyType"/> class.
    /// </summary>
    /// <param name="keyType">The runtime <see cref="SecretKey"/> type of the keys (for example the
    /// <see cref="EccSecretKey"/> type); a key whose type derives from it is accepted.</param>
    /// <param name="keyBitSizes">The sizes, in bits, of the keys of this type that the algorithm accepts.</param>
    /// <exception cref="ArgumentException"><paramref name="keyType"/> does not derive from
    /// <see cref="SecretKey"/>.</exception>
    public AlgorithmKeyType(Type keyType, IEnumerable<KeySizes> keyBitSizes)
    {
        ArgumentNullException.ThrowIfNull(keyType);
        ArgumentNullException.ThrowIfNull(keyBitSizes);

        if (!typeof(SecretKey).IsAssignableFrom(keyType))
        {
            throw new ArgumentException(
                $"The key type '{keyType.FullName}' does not derive from '{typeof(SecretKey).FullName}'.",
                nameof(keyType)
            );
        }

        KeyType = keyType;
        KeyBitSizes = [.. keyBitSizes];
    }

    /// <summary>
    /// Gets the runtime <see cref="SecretKey"/> type of the keys; a key whose type derives from it is accepted.
    /// </summary>
    public Type KeyType { get; }

    /// <summary>
    /// Gets the sizes, in bits, of the keys of this type that the algorithm accepts.
    /// </summary>
    public IReadOnlyList<KeySizes> KeyBitSizes { get; }

    /// <summary>
    /// Determines whether keys of the specified runtime type are of this key type.
    /// </summary>
    /// <param name="keyType">The runtime type of a key.</param>
    /// <returns><c>true</c> when <paramref name="keyType"/> derives from <see cref="KeyType"/>; otherwise,
    /// <c>false</c>.</returns>
    public bool Accepts(Type keyType) => KeyType.IsAssignableFrom(keyType);

    /// <summary>
    /// Determines whether a key of this type may have the specified size.
    /// </summary>
    /// <param name="keySizeBits">The size, in bits, of a key.</param>
    /// <returns><c>true</c> when the size is one of <see cref="KeyBitSizes"/>; otherwise, <c>false</c>.</returns>
    public bool IsLegalSize(int keySizeBits) =>
        KeySizesUtility.IsLegalSize(KeyBitSizes, keySizeBits);
}
