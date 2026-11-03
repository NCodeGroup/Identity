// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// One OKP (Octet Key Pair) curve (RFC 8037): its JWK <c>crv</c> name, its X.509 OID (RFC 8410), the fixed byte sizes
/// of its private and public keys, and the operations its keys support. RFC 8037 gives every OKP curve one use, which
/// its kind carries: an <see cref="EdwardsOkpCurve"/> signs with <c>EdDSA</c> (§3.1) and a
/// <see cref="MontgomeryOkpCurve"/> agrees keys with <c>ECDH-ES</c> (§3.2), so a key of one use has no operation of the
/// other. The private key is the seed of an Edwards curve or the scalar of a Montgomery curve.
/// </summary>
/// <param name="name">The JWK <c>crv</c> name of the curve.</param>
/// <param name="oid">The X.509 algorithm OID of the curve (RFC 8410 §3).</param>
/// <param name="privateKeySize">The size, in bytes, of a private key.</param>
/// <param name="publicKeySize">The size, in bytes, of a public key.</param>
// See ADR-1390.
internal abstract class OkpCurve(string name, string oid, int privateKeySize, int publicKeySize)
{
    /// <summary>
    /// Gets the JWK <c>crv</c> name of the curve.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the X.509 algorithm OID of the curve (RFC 8410 §3).
    /// </summary>
    public string Oid { get; } = oid;

    /// <summary>
    /// Gets the size, in bytes, of a private key.
    /// </summary>
    public int PrivateKeySize { get; } = privateKeySize;

    /// <summary>
    /// Gets the size, in bytes, of a public key.
    /// </summary>
    public int PublicKeySize { get; } = publicKeySize;

    /// <summary>
    /// Gets the size, in bits, of a key on the curve, the size of its public key.
    /// </summary>
    public int KeySizeBits => PublicKeySize * 8;

    /// <summary>
    /// Derives the public key from the private key: the encoded point <c>[s]B</c> of an Edwards curve, or the
    /// u-coordinate <c>X25519(k, 9)</c> / <c>X448(k, 5)</c> of a Montgomery curve.
    /// </summary>
    /// <param name="privateKey">The private key, of <see cref="PrivateKeySize"/> bytes.</param>
    /// <param name="publicKey">The destination for the public key, of <see cref="PublicKeySize"/> bytes.</param>
    public abstract void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey);
}
