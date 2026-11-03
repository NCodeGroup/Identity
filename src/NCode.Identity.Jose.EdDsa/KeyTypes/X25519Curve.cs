// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using X25519Math = NCode.Identity.Jose.EdDsa.Math.X25519;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// The X25519 curve (RFC 7748 §5, OID <c>1.3.101.110</c>).
/// </summary>
internal sealed class X25519Curve()
    : MontgomeryOkpCurve(
        OkpCurves.X25519Name,
        "1.3.101.110",
        X25519Math.PrivateKeySize,
        X25519Math.PublicKeySize
    )
{
    /// <inheritdoc />
    public override void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey) =>
        X25519Math.GeneratePublicKey(privateKey, publicKey);

    /// <inheritdoc />
    public override bool TryAgree(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> sharedSecret
    ) => X25519Math.TryAgree(privateKey, peerPublicKey, sharedSecret);
}
