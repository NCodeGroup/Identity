// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// An Edwards OKP curve, whose keys sign with <c>EdDSA</c> (RFC 8037 §3.1): Ed25519 or Ed448.
/// </summary>
/// <param name="name">The JWK <c>crv</c> name of the curve.</param>
/// <param name="oid">The X.509 algorithm OID of the curve (RFC 8410 §3).</param>
/// <param name="privateKeySize">The size, in bytes, of a private key (the seed).</param>
/// <param name="publicKeySize">The size, in bytes, of a public key.</param>
/// <param name="signatureSize">The size, in bytes, of a signature.</param>
internal abstract class EdwardsOkpCurve(
    string name,
    string oid,
    int privateKeySize,
    int publicKeySize,
    int signatureSize
) : OkpCurve(name, oid, privateKeySize, publicKeySize)
{
    /// <summary>
    /// Gets the size, in bytes, of a signature.
    /// </summary>
    public int SignatureSize { get; } = signatureSize;

    /// <summary>
    /// Signs a message with the private key's seed.
    /// </summary>
    /// <param name="seed">The private key's seed, of <see cref="OkpCurve.PrivateKeySize"/> bytes.</param>
    /// <param name="message">The message to sign.</param>
    /// <param name="signature">The destination for the signature, of <see cref="SignatureSize"/> bytes.</param>
    public abstract void Sign(
        ReadOnlySpan<byte> seed,
        ReadOnlySpan<byte> message,
        Span<byte> signature
    );

    /// <summary>
    /// Verifies a signature of a message with the public key.
    /// </summary>
    /// <param name="publicKey">The public key, of <see cref="OkpCurve.PublicKeySize"/> bytes.</param>
    /// <param name="message">The message that was signed.</param>
    /// <param name="signature">The signature to verify.</param>
    /// <returns><c>true</c> when the signature is valid; otherwise, <c>false</c>.</returns>
    public abstract bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature
    );

    /// <summary>
    /// Gets the hash function of the OpenID Connect token hashes (<c>at_hash</c>, <c>c_hash</c>) for the curve.
    /// </summary>
    /// <param name="hashSizeBytes">When this method returns, the size, in bytes, of the hash.</param>
    /// <returns>The hash function.</returns>
    public abstract HashFunctionDelegate GetTokenHashFunction(out int hashSizeBytes);
}
