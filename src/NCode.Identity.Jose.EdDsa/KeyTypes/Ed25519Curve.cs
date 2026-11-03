// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using Ed25519Math = NCode.Identity.Jose.EdDsa.Math.Ed25519;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// The Ed25519 curve (RFC 8032 §5.1, OID <c>1.3.101.112</c>).
/// </summary>
internal sealed class Ed25519Curve()
    : EdwardsOkpCurve(
        OkpCurves.Ed25519Name,
        "1.3.101.112",
        privateKeySize: 32,
        publicKeySize: 32,
        signatureSize: Ed25519Math.SignatureSize
    )
{
    /// <inheritdoc />
    public override void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey) =>
        Ed25519Math.GeneratePublicKey(privateKey, publicKey);

    /// <inheritdoc />
    public override void Sign(
        ReadOnlySpan<byte> seed,
        ReadOnlySpan<byte> message,
        Span<byte> signature
    ) => Ed25519Math.Sign(seed, message, signature);

    /// <inheritdoc />
    public override bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature
    ) => Ed25519Math.Verify(publicKey, message, signature);

    /// <inheritdoc />
    public override HashFunctionDelegate GetTokenHashFunction(out int hashSizeBytes)
    {
        hashSizeBytes = SHA512.HashSizeInBytes;
        return SHA512.TryHashData;
    }
}
