// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using Ed448Math = NCode.Identity.Jose.EdDsa.Math.Ed448;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// The Ed448 curve (RFC 8032 §5.2, OID <c>1.3.101.113</c>).
/// </summary>
internal sealed class Ed448Curve()
    : EdwardsOkpCurve(
        OkpCurves.Ed448Name,
        "1.3.101.113",
        privateKeySize: 57,
        publicKeySize: 57,
        signatureSize: Ed448Math.SignatureSize
    )
{
    // The Ed448 ID-token hash is SHAKE256 with a 114-octet output (OIDF Connect issue #1125).
    private const int TokenHashSizeBytes = 114;

    private static bool TryShake256TokenHash(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        out int bytesWritten
    )
    {
        if (destination.Length < TokenHashSizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        Shake256.HashData(source, destination[..TokenHashSizeBytes]);
        bytesWritten = TokenHashSizeBytes;
        return true;
    }

    /// <inheritdoc />
    public override void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey) =>
        Ed448Math.GeneratePublicKey(privateKey, publicKey);

    /// <inheritdoc />
    public override void Sign(
        ReadOnlySpan<byte> seed,
        ReadOnlySpan<byte> message,
        Span<byte> signature
    ) => Ed448Math.Sign(seed, message, signature);

    /// <inheritdoc />
    public override bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature
    ) => Ed448Math.Verify(publicKey, message, signature);

    /// <inheritdoc />
    public override HashFunctionDelegate GetTokenHashFunction(out int hashSizeBytes)
    {
        hashSizeBytes = TokenHashSizeBytes;
        return TryShake256TokenHash;
    }
}
