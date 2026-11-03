// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
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
// See ADR-1389 and ADR-1391.
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
    /// Gets the curves that keys of this type must be on for the algorithm, in the algorithm's order of preference (for
    /// example <c>P-256</c> for <c>ES256</c>, <c>secp256k1</c> for <c>ES256K</c>, <c>Ed25519</c> and <c>Ed448</c> for
    /// <c>EdDSA</c>, or <c>X25519</c> and <c>X448</c> for <c>ECDH-ES</c> over <c>OKP</c> keys), or an empty list when
    /// the type has no curve. Two curves can share a key size, so a key is generated for the algorithm on the first of
    /// these curves that has the key's size, and a key on any other curve is refused when it is declared for the
    /// algorithm.
    /// </summary>
    public IReadOnlyList<AlgorithmCurve> Curves
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = [.. value];
        }
    } = [];

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

    /// <summary>
    /// Determines whether a key of this type and of the specified size may be on the specified curve.
    /// </summary>
    /// <param name="curve">The <c>JWK</c> <c>crv</c> name of a key's curve.</param>
    /// <param name="keySizeBits">The size, in bits, of the key.</param>
    /// <returns><c>true</c> when <see cref="Curves"/> is empty, or names <paramref name="curve"/> with keys of
    /// <paramref name="keySizeBits"/>; otherwise, <c>false</c>.</returns>
    public bool IsLegalCurve(string curve, int keySizeBits)
    {
        ArgumentNullException.ThrowIfNull(curve);

        return Curves.Count == 0
            || Curves.Any(legal =>
                string.Equals(legal.Name, curve, StringComparison.Ordinal)
                && legal.KeySizeBits == keySizeBits
            );
    }

    /// <summary>
    /// Gets the first of <see cref="Curves"/> whose keys have the specified size, the curve a key of that size is
    /// generated on for the algorithm.
    /// </summary>
    /// <param name="keySizeBits">The size, in bits, of the key.</param>
    /// <param name="curve">When this method returns <c>true</c>, the curve.</param>
    /// <returns><c>true</c> when one of <see cref="Curves"/> has keys of the size; otherwise, <c>false</c>.</returns>
    public bool TryGetCurve(int keySizeBits, [NotNullWhen(true)] out AlgorithmCurve? curve)
    {
        curve = Curves.FirstOrDefault(candidate => candidate.KeySizeBits == keySizeBits);
        return curve is not null;
    }
}
