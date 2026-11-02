// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Jose.EdDsa.Math;

/// <summary>
/// The X448 Diffie-Hellman function over Curve448 (RFC 7748 §5), used by <c>ECDH-ES</c> with <c>OKP</c> keys
/// (RFC 8037 §3.2). The scalar multiplication is the Montgomery ladder of RFC 7748 §5 over the clean-room
/// <see cref="Fe448"/> field: every iteration performs the same field operations and swaps with a masked conditional
/// swap, so it is constant-time with respect to the scalar.
/// </summary>
internal static class X448
{
    public const int PrivateKeySize = 56;
    public const int PublicKeySize = 56;
    public const int SharedSecretSize = 56;

    private const int ScalarBits = 448;

    // (A - 2) / 4 for Curve448, whose A is 156326 (RFC 7748 §5).
    private const ulong A24 = 39081;

    // The u-coordinate of the base point, 5 (RFC 7748 §4.2).
    private static ReadOnlySpan<byte> BasePoint =>
        [
            5,
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
    /// Derives the 56-byte public key <c>X448(k, 5)</c> from the 56-byte private key.
    /// </summary>
    public static void GeneratePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey) =>
        ScalarMult(privateKey, BasePoint, publicKey);

    /// <summary>
    /// Computes the shared secret <c>X448(k, u)</c> of the private key and the peer's public key. Returns
    /// <c>false</c> when the shared secret is all zeros, which happens exactly when the peer's public key is a point
    /// of small order, and which the caller must refuse (RFC 7748 §6.2, RFC 8037 §4).
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
    /// The X448 function of RFC 7748 §5: decodes the scalar (clamping it) and the u-coordinate (accepting a
    /// non-canonical value as if reduced), and writes the 56-byte u-coordinate of the scalar multiple.
    /// </summary>
    public static void ScalarMult(
        ReadOnlySpan<byte> scalar,
        ReadOnlySpan<byte> u,
        Span<byte> output
    )
    {
        if (scalar.Length != PrivateKeySize)
        {
            throw new ArgumentException("An X448 scalar is 56 bytes.", nameof(scalar));
        }

        if (u.Length != PublicKeySize)
        {
            throw new ArgumentException("An X448 u-coordinate is 56 bytes.", nameof(u));
        }

        if (output.Length < SharedSecretSize)
        {
            throw new ArgumentException("The output must hold 56 bytes.", nameof(output));
        }

        Span<byte> k = stackalloc byte[PrivateKeySize];
        try
        {
            scalar.CopyTo(k);
            k[0] &= 252;
            k[55] |= 128;

            var a24 = Fe448.FromUInt64(A24);
            var x1 = Fe448.FromBytes(u);
            var x2 = Fe448.One;
            var z2 = Fe448.Zero;
            var x3 = x1;
            var z3 = Fe448.One;
            ulong swap = 0;

            for (var t = ScalarBits - 1; t >= 0; t--)
            {
                var kt = (ulong)((k[t >> 3] >> (t & 7)) & 1);
                swap ^= kt;
                Fe448.CSwap(ref x2, ref x3, swap);
                Fe448.CSwap(ref z2, ref z3, swap);
                swap = kt;

                var a = Fe448.Add(x2, z2);
                var aa = Fe448.Sqr(a);
                var b = Fe448.Sub(x2, z2);
                var bb = Fe448.Sqr(b);
                var e = Fe448.Sub(aa, bb);
                var c = Fe448.Add(x3, z3);
                var d = Fe448.Sub(x3, z3);
                var da = Fe448.Mul(d, a);
                var cb = Fe448.Mul(c, b);
                x3 = Fe448.Sqr(Fe448.Add(da, cb));
                z3 = Fe448.Mul(x1, Fe448.Sqr(Fe448.Sub(da, cb)));
                x2 = Fe448.Mul(aa, bb);
                z2 = Fe448.Mul(e, Fe448.Add(aa, Fe448.Mul(a24, e)));
            }

            Fe448.CSwap(ref x2, ref x3, swap);
            Fe448.CSwap(ref z2, ref z3, swap);

            Fe448.Mul(x2, Fe448.Invert(z2)).ToBytes(output);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(k);
        }
    }
}
