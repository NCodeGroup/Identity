// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using X448Math = NCode.Identity.Jose.EdDsa.Math.X448;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// The X448 curve (RFC 7748 §5, OID <c>1.3.101.111</c>).
/// </summary>
internal sealed class X448Curve()
    : MontgomeryOkpCurve(
        OkpCurves.X448Name,
        "1.3.101.111",
        X448Math.PrivateKeySize,
        X448Math.PublicKeySize
    )
{
    /// <inheritdoc />
    public override void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey) =>
        X448Math.GeneratePublicKey(privateKey, publicKey);

    /// <inheritdoc />
    public override bool TryAgree(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> sharedSecret
    ) => X448Math.TryAgree(privateKey, peerPublicKey, sharedSecret);
}
