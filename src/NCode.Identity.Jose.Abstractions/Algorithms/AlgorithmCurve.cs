// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes one curve that an <see cref="AlgorithmKeyType"/> accepts keys on, by its <c>JWK</c> <c>crv</c> name and
/// the size of its keys. Two curves can share a size, as P-256 and secp256k1 or Ed25519 and X25519 do, so a curve is
/// told apart by its name, and a key's size is checked against its curve's.
/// </summary>
// See ADR-1389.
[PublicAPI]
public sealed class AlgorithmCurve
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmCurve"/> class.
    /// </summary>
    /// <param name="name">The <c>JWK</c> <c>crv</c> name of the curve (for example <c>P-256</c> or
    /// <c>X25519</c>).</param>
    /// <param name="keySizeBits">The size, in bits, of keys on the curve.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="keySizeBits"/> is not positive.</exception>
    public AlgorithmCurve(string name, int keySizeBits)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keySizeBits);

        Name = name;
        KeySizeBits = keySizeBits;
    }

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> name of the curve.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the size, in bits, of keys on the curve.
    /// </summary>
    public int KeySizeBits { get; }
}
