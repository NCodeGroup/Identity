// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Jose.EdDsa.Math;

/// <summary>
/// The X25519 Diffie-Hellman function over Curve25519 (RFC 7748 §5), used by <c>ECDH-ES</c> with <c>OKP</c> keys
/// (RFC 8037 §3.2). The scalar multiplication is the Montgomery ladder of RFC 7748 §5 over the clean-room
/// <see cref="Fe25519"/> field: every iteration performs the same field operations and swaps with a masked
/// conditional swap, so it is constant-time with respect to the scalar.
/// </summary>
internal static class X25519
{
    public const int PrivateKeySize = 32;
    public const int PublicKeySize = 32;
    public const int SharedSecretSize = 32;

    private const int ScalarBits = 255;

    // The u-coordinate of the base point, 9 (RFC 7748 §4.1).
    private static ReadOnlySpan<byte> BasePoint =>
        [
            9,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
        ];

    /// <summary>
    /// Derives the 32-byte public key <c>X25519(k, 9)</c> from the 32-byte private key.
    /// </summary>
    public static void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey) =>
        ScalarMult(privateKey, BasePoint, publicKey);

    /// <summary>
    /// Computes the shared secret <c>X25519(k, u)</c> of the private key and the peer's public key. Returns
    /// <c>false</c> when the shared secret is all zeros, which happens exactly when the peer's public key is a point
    /// of small order, and which the caller must refuse (RFC 7748 §6.1, RFC 8037 §4).
    /// </summary>
    public static bool TryAgree(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> sharedSecret
    )
    {
        ScalarMult(privateKey, peerPublicKey, sharedSecret);
        return XdhUtility.IsNonZero(sharedSecret[..SharedSecretSize]);
    }

    /// <summary>
    /// The X25519 function of RFC 7748 §5: decodes the scalar (clamping it) and the u-coordinate (masking its most
    /// significant bit, and accepting a non-canonical value as if reduced), and writes the 32-byte u-coordinate of the
    /// scalar multiple.
    /// </summary>
    public static void ScalarMult(
        ReadOnlySpan<byte> scalar,
        ReadOnlySpan<byte> u,
        Span<byte> output
    )
    {
        if (scalar.Length != PrivateKeySize)
        {
            throw new ArgumentException("An X25519 scalar is 32 bytes.", nameof(scalar));
        }

        if (u.Length != PublicKeySize)
        {
            throw new ArgumentException("An X25519 u-coordinate is 32 bytes.", nameof(u));
        }

        if (output.Length < SharedSecretSize)
        {
            throw new ArgumentException("The output must hold 32 bytes.", nameof(output));
        }

        Span<byte> k = stackalloc byte[PrivateKeySize];
        try
        {
            scalar.CopyTo(k);
            k[0] &= 248;
            k[31] &= 127;
            k[31] |= 64;

            // Fe25519.FromBytes ignores bit 255, which is the masking RFC 7748 §5 requires.
            var x1 = Fe25519.FromBytes(u);
            var x2 = Fe25519.One;
            var z2 = Fe25519.Zero;
            var x3 = x1;
            var z3 = Fe25519.One;
            ulong swap = 0;

            for (var t = ScalarBits - 1; t >= 0; t--)
            {
                var kt = (ulong)((k[t >> 3] >> (t & 7)) & 1);
                swap ^= kt;
                Fe25519.CSwap(ref x2, ref x3, swap);
                Fe25519.CSwap(ref z2, ref z3, swap);
                swap = kt;

                var a = Fe25519.Add(x2, z2);
                var aa = Fe25519.Sqr(a);
                var b = Fe25519.Sub(x2, z2);
                var bb = Fe25519.Sqr(b);
                var e = Fe25519.Sub(aa, bb);
                var c = Fe25519.Add(x3, z3);
                var d = Fe25519.Sub(x3, z3);
                var da = Fe25519.Mul(d, a);
                var cb = Fe25519.Mul(c, b);
                x3 = Fe25519.Sqr(Fe25519.Add(da, cb));
                z3 = Fe25519.Mul(x1, Fe25519.Sqr(Fe25519.Sub(da, cb)));
                x2 = Fe25519.Mul(aa, bb);
                // z2 = E * (AA + a24 * E) with a24 = 121665; since AA = BB + E, that is E * (BB + 121666 * E).
                z2 = Fe25519.Mul(e, Fe25519.Add(bb, Fe25519.Mul121666(e)));
            }

            Fe25519.CSwap(ref x2, ref x3, swap);
            Fe25519.CSwap(ref z2, ref z3, swap);

            Fe25519.Mul(x2, Fe25519.Invert(z2)).ToBytes(output);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(k);
        }
    }
}
