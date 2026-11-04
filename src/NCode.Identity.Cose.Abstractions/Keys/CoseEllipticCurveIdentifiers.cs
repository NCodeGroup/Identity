// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Contains the values of the IANA COSE Elliptic Curves registry that this library knows. The values are the known ones, never all of them: a curve is registered as a <see cref="CoseEllipticCurve"/>.
/// </summary>
[PublicAPI]
public static class CoseEllipticCurveIdentifiers
{
    /// <summary>
    /// The P-256 curve of <c>EC2</c> keys.
    /// </summary>
    public const int P256 = 1;

    /// <summary>
    /// The P-384 curve of <c>EC2</c> keys.
    /// </summary>
    public const int P384 = 2;

    /// <summary>
    /// The P-521 curve of <c>EC2</c> keys.
    /// </summary>
    public const int P521 = 3;

    /// <summary>
    /// The X25519 curve of <c>OKP</c> keys.
    /// </summary>
    public const int X25519 = 4;

    /// <summary>
    /// The X448 curve of <c>OKP</c> keys.
    /// </summary>
    public const int X448 = 5;

    /// <summary>
    /// The Ed25519 curve of <c>OKP</c> keys.
    /// </summary>
    public const int Ed25519 = 6;

    /// <summary>
    /// The Ed448 curve of <c>OKP</c> keys.
    /// </summary>
    public const int Ed448 = 7;

    /// <summary>
    /// The secp256k1 curve of <c>EC2</c> keys (RFC 8812).
    /// </summary>
    public const int Secp256k1 = 8;
}
