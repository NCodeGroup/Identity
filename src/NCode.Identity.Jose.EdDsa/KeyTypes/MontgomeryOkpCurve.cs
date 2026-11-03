// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// A Montgomery OKP curve, whose keys agree keys with <c>ECDH-ES</c> (RFC 8037 §3.2): X25519 or X448.
/// </summary>
/// <param name="name">The JWK <c>crv</c> name of the curve.</param>
/// <param name="oid">The X.509 algorithm OID of the curve (RFC 8410 §3).</param>
/// <param name="privateKeySize">The size, in bytes, of a private key (the scalar).</param>
/// <param name="publicKeySize">The size, in bytes, of a public key and of a shared secret.</param>
internal abstract class MontgomeryOkpCurve(
    string name,
    string oid,
    int privateKeySize,
    int publicKeySize
) : OkpCurve(name, oid, privateKeySize, publicKeySize)
{
    /// <summary>
    /// Agrees a shared secret of <see cref="OkpCurve.PublicKeySize"/> bytes from a private key and a peer's public key.
    /// </summary>
    /// <param name="privateKey">The private key, of <see cref="OkpCurve.PrivateKeySize"/> bytes.</param>
    /// <param name="peerPublicKey">The peer's public key, of <see cref="OkpCurve.PublicKeySize"/> bytes.</param>
    /// <param name="sharedSecret">The destination for the shared secret.</param>
    /// <returns><c>false</c> when the shared secret is all zeros (RFC 7748 §6); otherwise, <c>true</c>.</returns>
    public abstract bool TryAgree(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> sharedSecret
    );
}
