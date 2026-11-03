// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes one type of key that a <see cref="KeyedAlgorithm"/> accepts, together with the sizes, and for a type with
/// curves the curves, it accepts keys of that type on. An algorithm whose name serves several key types, such as
/// <c>ECDH-ES</c> over <c>EC</c> and <c>OKP</c> keys (RFC 8037 §3.2), lists one <see cref="AlgorithmKeyType"/> per
/// key type. A key type without curves, such as <c>RSA</c>, <c>oct</c> or <c>AKP</c>, is built with the constructor; a
/// key type whose keys are on curves is built with <see cref="OnCurves"/>, which takes the sizes from the curves.
/// </summary>
// See ADR-1389 and ADR-1392.
[PublicAPI]
public sealed class AlgorithmKeyType
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmKeyType"/> class for a key type without curves.
    /// </summary>
    /// <param name="keyClrType">The runtime <see cref="SecretKey"/> type of the keys (for example the
    /// <see cref="RsaSecretKey"/> type); a key whose type derives from it is accepted.</param>
    /// <param name="keyBitSizes">The sizes, in bits, of the keys of this type that the algorithm accepts.</param>
    /// <exception cref="ArgumentException"><paramref name="keyClrType"/> does not derive from
    /// <see cref="SecretKey"/>.</exception>
    public AlgorithmKeyType(Type keyClrType, IEnumerable<KeySizes> keyBitSizes)
        : this(keyClrType, [.. ValidateKeyBitSizes(keyBitSizes)], curves: []) { }

    private AlgorithmKeyType(Type keyClrType, KeySizes[] keyBitSizes, AlgorithmCurve[] curves)
    {
        ArgumentNullException.ThrowIfNull(keyClrType);

        if (!typeof(SecretKey).IsAssignableFrom(keyClrType))
        {
            throw new ArgumentException(
                $"The key type '{keyClrType.FullName}' does not derive from '{typeof(SecretKey).FullName}'.",
                nameof(keyClrType)
            );
        }

        KeyClrType = keyClrType;
        KeyBitSizes = keyBitSizes;
        Curves = curves;
    }

    /// <summary>
    /// Creates an <see cref="AlgorithmKeyType"/> whose keys must be on one of the specified curves. Its
    /// <see cref="KeyBitSizes"/> are the curves' sizes, so the sizes and the curves have one source.
    /// </summary>
    /// <param name="keyClrType">The runtime <see cref="SecretKey"/> type of the keys (for example the
    /// <see cref="EccSecretKey"/> type); a key whose type derives from it is accepted.</param>
    /// <param name="curves">The curves the keys must be on, in the algorithm's order of preference.</param>
    /// <returns>The new <see cref="AlgorithmKeyType"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="keyClrType"/> does not derive from
    /// <see cref="SecretKey"/>, <paramref name="curves"/> is empty, or two of its curves share a name.</exception>
    public static AlgorithmKeyType OnCurves(Type keyClrType, IEnumerable<AlgorithmCurve> curves)
    {
        ArgumentNullException.ThrowIfNull(curves);

        AlgorithmCurve[] list = [.. curves];
        if (list.Length == 0)
        {
            throw new ArgumentException(
                "A key type on curves names at least one curve.",
                nameof(curves)
            );
        }

        foreach (var curve in list)
        {
            ArgumentNullException.ThrowIfNull(curve, nameof(curves));
        }

        if (list.DistinctBy(curve => curve.Name, StringComparer.Ordinal).Count() != list.Length)
        {
            throw new ArgumentException("Two of the curves share a name.", nameof(curves));
        }

        KeySizes[] keyBitSizes =
        [
            .. list.Select(curve => curve.KeySizeBits)
                .Distinct()
                .Select(keySizeBits => new KeySizes(
                    minSize: keySizeBits,
                    maxSize: keySizeBits,
                    skipSize: 0
                )),
        ];

        return new AlgorithmKeyType(keyClrType, keyBitSizes, list);
    }

    private static IEnumerable<KeySizes> ValidateKeyBitSizes(IEnumerable<KeySizes> keyBitSizes)
    {
        ArgumentNullException.ThrowIfNull(keyBitSizes);
        return keyBitSizes;
    }

    /// <summary>
    /// Gets the runtime <see cref="SecretKey"/> type of the keys; a key whose type derives from it is accepted.
    /// </summary>
    public Type KeyClrType { get; }

    /// <summary>
    /// Gets the sizes, in bits, of the keys of this type that the algorithm accepts. For a key type on curves, these
    /// are the sizes of its <see cref="Curves"/>.
    /// </summary>
    public IReadOnlyList<KeySizes> KeyBitSizes { get; }

    /// <summary>
    /// Gets the curves that keys of this type must be on for the algorithm, in the algorithm's order of preference (for
    /// example <c>P-256</c> for <c>ES256</c>, <c>secp256k1</c> for <c>ES256K</c>, <c>Ed25519</c> and <c>Ed448</c> for
    /// <c>EdDSA</c>, or <c>X25519</c> and <c>X448</c> for <c>ECDH-ES</c> over <c>OKP</c> keys), or an empty list when
    /// the type has no curve. Two curves can share a key size, so a key is checked by its curve's name.
    /// </summary>
    public IReadOnlyList<AlgorithmCurve> Curves { get; }

    /// <summary>
    /// Determines whether keys of the specified runtime type are of this key type.
    /// </summary>
    /// <param name="keyClrType">The runtime type of a key.</param>
    /// <returns><c>true</c> when <paramref name="keyClrType"/> derives from <see cref="KeyClrType"/>; otherwise,
    /// <c>false</c>.</returns>
    public bool Accepts(Type keyClrType) => KeyClrType.IsAssignableFrom(keyClrType);

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
    /// <param name="curve">The <c>JWK</c> <c>crv</c> name of a key's curve, or <c>null</c> when the key has no named
    /// curve.</param>
    /// <param name="keySizeBits">The size, in bits, of the key.</param>
    /// <returns><c>true</c> when <see cref="Curves"/> is empty, or names <paramref name="curve"/> with keys of
    /// <paramref name="keySizeBits"/>; otherwise, <c>false</c>.</returns>
    public bool IsLegalCurve(string? curve, int keySizeBits) =>
        Curves.Count == 0
        || (
            curve is not null
            && Curves.Any(legal =>
                string.Equals(legal.Name, curve, StringComparison.Ordinal)
                && legal.KeySizeBits == keySizeBits
            )
        );

    /// <summary>
    /// Determines whether a key is of this key type: its runtime type is <see cref="KeyClrType"/>, its size is one of
    /// <see cref="KeyBitSizes"/>, and, for a key type on curves, its <see cref="AsymmetricSecretKey.CurveName"/> is one
    /// of <see cref="Curves"/> with that size. It is the one rule that algorithms and key agreements check a key's
    /// type, size and curve by.
    /// </summary>
    /// <param name="secretKey">The key to check.</param>
    /// <returns><c>true</c> when the key is of this key type; otherwise, <c>false</c>.</returns>
    public bool IsKeyCompatible(SecretKey secretKey)
    {
        ArgumentNullException.ThrowIfNull(secretKey);

        return Accepts(secretKey.GetType())
            && IsLegalSize(secretKey.KeySizeBits)
            && IsLegalCurve((secretKey as AsymmetricSecretKey)?.CurveName, secretKey.KeySizeBits);
    }

    /// <summary>
    /// Gets the first of <see cref="Curves"/> whose keys have the specified size, the curve a key of that size is
    /// generated on for the algorithm when no curve is named.
    /// </summary>
    /// <param name="keySizeBits">The size, in bits, of the key.</param>
    /// <param name="curve">When this method returns <c>true</c>, the curve.</param>
    /// <returns><c>true</c> when one of <see cref="Curves"/> has keys of the size; otherwise, <c>false</c>.</returns>
    public bool TryGetCurve(int keySizeBits, [NotNullWhen(true)] out AlgorithmCurve? curve)
    {
        curve = Curves.FirstOrDefault(candidate => candidate.KeySizeBits == keySizeBits);
        return curve is not null;
    }

    /// <summary>
    /// Gets the one of <see cref="Curves"/> with the specified <c>JWK</c> <c>crv</c> name.
    /// </summary>
    /// <param name="name">The <c>JWK</c> <c>crv</c> name of the curve.</param>
    /// <param name="curve">When this method returns <c>true</c>, the curve.</param>
    /// <returns><c>true</c> when one of <see cref="Curves"/> has the name; otherwise, <c>false</c>.</returns>
    public bool TryGetCurve(string name, [NotNullWhen(true)] out AlgorithmCurve? curve)
    {
        ArgumentNullException.ThrowIfNull(name);

        curve = Curves.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.Ordinal)
        );
        return curve is not null;
    }
}
