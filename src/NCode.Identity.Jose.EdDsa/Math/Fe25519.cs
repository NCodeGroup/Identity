// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace NCode.Identity.Jose.EdDsa.Math;

/// <summary>
/// Element of the prime field <c>GF(2^255 - 19)</c> represented as five 51-bit limbs (radix 2^51) stored in
/// <see cref="ulong"/>s, with <see cref="System.UInt128"/> used for products. All operations are branch-free and have
/// no data-dependent memory access, so they are constant-time with respect to the field value. Clean-room
/// implementation of the field defined by RFC 7748 §5 / RFC 8032 §5.1 using the public-domain radix-2^51 formulas.
/// </summary>
internal struct Fe25519
{
    private const ulong Mask = (1UL << 51) - 1;

    // Hot inner-loop math: force full JIT optimization and inlining. Constant-time is preserved because none of
    // these methods branch on or index by field values. See ADR-1116 / the crypto-math performance convention.
    private const MethodImplOptions Opt =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    private ulong L0 { get; set; }
    private ulong L1 { get; set; }
    private ulong L2 { get; set; }
    private ulong L3 { get; set; }
    private ulong L4 { get; set; }

    public static Fe25519 Zero => default;

    public static Fe25519 One => new() { L0 = 1 };

    /// <summary>Creates a field element from a small unsigned integer (the value must be below 2^51).</summary>
    [MethodImpl(Opt)]
    public static Fe25519 FromUInt64(ulong value) => new() { L0 = value };

    /// <summary>A fixed square root of <c>-1</c> modulo <c>p</c>, used to correct the sign during point decompression.</summary>
    public static Fe25519 SqrtMinusOne { get; } = Pow(FromUInt64(2), PMinus1Div4BitsBE);

    [MethodImpl(Opt)]
    public static Fe25519 Add(in Fe25519 a, in Fe25519 b)
    {
        var r = new Fe25519
        {
            L0 = a.L0 + b.L0,
            L1 = a.L1 + b.L1,
            L2 = a.L2 + b.L2,
            L3 = a.L3 + b.L3,
            L4 = a.L4 + b.L4,
        };
        r.WeakReduce();
        return r;
    }

    [MethodImpl(Opt)]
    public static Fe25519 Sub(in Fe25519 a, in Fe25519 b)
    {
        // Add 2*p (which is 0 mod p) before subtracting so each limb stays non-negative; 2p limbs are
        // 2*(2^51 - 19) for limb 0 and 2*(2^51 - 1) for limbs 1..4.
        var r = new Fe25519
        {
            L0 = a.L0 + 0x0F_FFFF_FFFF_FFDAUL - b.L0,
            L1 = a.L1 + 0x0F_FFFF_FFFF_FFFEUL - b.L1,
            L2 = a.L2 + 0x0F_FFFF_FFFF_FFFEUL - b.L2,
            L3 = a.L3 + 0x0F_FFFF_FFFF_FFFEUL - b.L3,
            L4 = a.L4 + 0x0F_FFFF_FFFF_FFFEUL - b.L4,
        };
        r.WeakReduce();
        return r;
    }

    [MethodImpl(Opt)]
    public static Fe25519 Neg(in Fe25519 a) => Sub(Zero, a);

    [MethodImpl(Opt)]
    public static Fe25519 Mul(in Fe25519 a, in Fe25519 b)
    {
        // 19*b_i stays below 2^56; a_i below 2^52; each UInt128 accumulator sums five products below 2^108.
        ulong b1_19 = 19 * b.L1;
        ulong b2_19 = 19 * b.L2;
        ulong b3_19 = 19 * b.L3;
        ulong b4_19 = 19 * b.L4;

        UInt128 t0 =
            (UInt128)a.L0 * b.L0
            + (UInt128)a.L1 * b4_19
            + (UInt128)a.L2 * b3_19
            + (UInt128)a.L3 * b2_19
            + (UInt128)a.L4 * b1_19;
        UInt128 t1 =
            (UInt128)a.L0 * b.L1
            + (UInt128)a.L1 * b.L0
            + (UInt128)a.L2 * b4_19
            + (UInt128)a.L3 * b3_19
            + (UInt128)a.L4 * b2_19;
        UInt128 t2 =
            (UInt128)a.L0 * b.L2
            + (UInt128)a.L1 * b.L1
            + (UInt128)a.L2 * b.L0
            + (UInt128)a.L3 * b4_19
            + (UInt128)a.L4 * b3_19;
        UInt128 t3 =
            (UInt128)a.L0 * b.L3
            + (UInt128)a.L1 * b.L2
            + (UInt128)a.L2 * b.L1
            + (UInt128)a.L3 * b.L0
            + (UInt128)a.L4 * b4_19;
        UInt128 t4 =
            (UInt128)a.L0 * b.L4
            + (UInt128)a.L1 * b.L3
            + (UInt128)a.L2 * b.L2
            + (UInt128)a.L3 * b.L1
            + (UInt128)a.L4 * b.L0;

        return CarryReduce(t0, t1, t2, t3, t4);
    }

    // Dedicated squaring: the symmetric cross terms are doubled once, saving multiplications versus Mul(a, a).
    [MethodImpl(Opt)]
    public static Fe25519 Sqr(in Fe25519 a)
    {
        ulong a0 = a.L0,
            a1 = a.L1,
            a2 = a.L2,
            a3 = a.L3,
            a4 = a.L4;
        var a0_2 = a0 * 2;
        var a1_2 = a1 * 2;
        var a2_38 = a2 * 2 * 19;
        var a4_19 = a4 * 19;
        var a4_38 = a4_19 * 2;

        var t0 = (UInt128)a0 * a0 + (UInt128)a4_38 * a1 + (UInt128)a2_38 * a3;
        var t1 = (UInt128)a0_2 * a1 + (UInt128)a4_38 * a2 + (UInt128)a3 * (a3 * 19);
        var t2 = (UInt128)a0_2 * a2 + (UInt128)a1 * a1 + (UInt128)a4_38 * a3;
        var t3 = (UInt128)a0_2 * a3 + (UInt128)a1_2 * a2 + (UInt128)a4 * a4_19;
        var t4 = (UInt128)a0_2 * a4 + (UInt128)a1_2 * a3 + (UInt128)a2 * a2;

        return CarryReduce(t0, t1, t2, t3, t4);
    }

    /// <summary>Multiplies by the small constant 121666 (used by the Montgomery ladder / curve constant).</summary>
    public static Fe25519 Mul121666(in Fe25519 a)
    {
        UInt128 t0 = (UInt128)a.L0 * 121666;
        UInt128 t1 = (UInt128)a.L1 * 121666;
        UInt128 t2 = (UInt128)a.L2 * 121666;
        UInt128 t3 = (UInt128)a.L3 * 121666;
        UInt128 t4 = (UInt128)a.L4 * 121666;
        return CarryReduce(t0, t1, t2, t3, t4);
    }

    [MethodImpl(Opt)]
    private static Fe25519 CarryReduce(UInt128 t0, UInt128 t1, UInt128 t2, UInt128 t3, UInt128 t4)
    {
        t1 += (ulong)(t0 >> 51);
        var r0 = (ulong)t0 & Mask;
        t2 += (ulong)(t1 >> 51);
        var r1 = (ulong)t1 & Mask;
        t3 += (ulong)(t2 >> 51);
        var r2 = (ulong)t2 & Mask;
        t4 += (ulong)(t3 >> 51);
        var r3 = (ulong)t3 & Mask;
        r0 += 19 * (ulong)(t4 >> 51);
        var r4 = (ulong)t4 & Mask;
        r1 += r0 >> 51;
        r0 &= Mask;
        return new Fe25519
        {
            L0 = r0,
            L1 = r1,
            L2 = r2,
            L3 = r3,
            L4 = r4,
        };
    }

    // Single carry-propagation pass; leaves limbs below 2^51 except possibly limb 1 which stays below 2^52.
    [MethodImpl(Opt)]
    private void WeakReduce()
    {
        L1 += L0 >> 51;
        L0 &= Mask;
        L2 += L1 >> 51;
        L1 &= Mask;
        L3 += L2 >> 51;
        L2 &= Mask;
        L4 += L3 >> 51;
        L3 &= Mask;
        L0 += 19 * (L4 >> 51);
        L4 &= Mask;
        L1 += L0 >> 51;
        L0 &= Mask;
    }

    /// <summary>Computes the multiplicative inverse <c>a^(p-2)</c> (and <c>0</c> for <c>0</c>).</summary>
    public static Fe25519 Invert(in Fe25519 a) => Pow(a, PMinus2BitsBE);

    /// <summary>Computes <c>a^((p-5)/8)</c>, used when taking square roots during point decompression.</summary>
    public static Fe25519 PowPMinus5Div8(in Fe25519 a) => Pow(a, PMinus5Div8BitsBE);

    // Left-to-right square-and-multiply over a fixed, public exponent: constant-time w.r.t. the (secret) base
    // because the squaring/multiplication schedule depends only on the compile-time exponent constant.
    private static Fe25519 Pow(in Fe25519 a, ReadOnlySpan<byte> exponentBigEndian)
    {
        var result = One;
        var started = false;
        foreach (var octet in exponentBigEndian)
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                if (started)
                {
                    result = Sqr(result);
                }

                if (((octet >> bit) & 1) == 1)
                {
                    result = started ? Mul(result, a) : a;
                    started = true;
                }
            }
        }

        return result;
    }

    // p - 2 = 2^255 - 21.
    private static ReadOnlySpan<byte> PMinus2BitsBE =>
        [
            0x7F,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xEB,
        ];

    // (p - 5) / 8 = 2^252 - 3.
    private static ReadOnlySpan<byte> PMinus5Div8BitsBE =>
        [
            0x0F,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFD,
        ];

    // (p - 1) / 4 = 2^253 - 5.
    private static ReadOnlySpan<byte> PMinus1Div4BitsBE =>
        [
            0x1F,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFB,
        ];

    /// <summary>Constant-time conditional move: sets <paramref name="r"/> to <paramref name="a"/> when
    /// <paramref name="move"/> is 1, leaving it unchanged when 0.</summary>
    [MethodImpl(Opt)]
    public static void CMov(ref Fe25519 r, in Fe25519 a, ulong move)
    {
        var mask = 0UL - move;
        r.L0 ^= mask & (r.L0 ^ a.L0);
        r.L1 ^= mask & (r.L1 ^ a.L1);
        r.L2 ^= mask & (r.L2 ^ a.L2);
        r.L3 ^= mask & (r.L3 ^ a.L3);
        r.L4 ^= mask & (r.L4 ^ a.L4);
    }

    /// <summary>Constant-time conditional swap of <paramref name="a"/> and <paramref name="b"/> when
    /// <paramref name="swap"/> is 1.</summary>
    [MethodImpl(Opt)]
    public static void CSwap(ref Fe25519 a, ref Fe25519 b, ulong swap)
    {
        var mask = 0UL - swap;
        var t0 = mask & (a.L0 ^ b.L0);
        a.L0 ^= t0;
        b.L0 ^= t0;
        var t1 = mask & (a.L1 ^ b.L1);
        a.L1 ^= t1;
        b.L1 ^= t1;
        var t2 = mask & (a.L2 ^ b.L2);
        a.L2 ^= t2;
        b.L2 ^= t2;
        var t3 = mask & (a.L3 ^ b.L3);
        a.L3 ^= t3;
        b.L3 ^= t3;
        var t4 = mask & (a.L4 ^ b.L4);
        a.L4 ^= t4;
        b.L4 ^= t4;
    }

    /// <summary>Loads a field element from 32 little-endian bytes, ignoring the most-significant bit (bit 255).</summary>
    public static Fe25519 FromBytes(ReadOnlySpan<byte> source)
    {
        return new Fe25519
        {
            L0 = ReadBits(source, 0, 51),
            L1 = ReadBits(source, 51, 51),
            L2 = ReadBits(source, 102, 51),
            L3 = ReadBits(source, 153, 51),
            L4 = ReadBits(source, 204, 51),
        };
    }

    /// <summary>Writes the canonical (fully reduced, in <c>[0, p)</c>) little-endian 32-byte encoding.</summary>
    public readonly void ToBytes(Span<byte> destination)
    {
        ulong t0 = L0,
            t1 = L1,
            t2 = L2,
            t3 = L3,
            t4 = L4;

        // Fully carry-propagate.
        t1 += t0 >> 51;
        t0 &= Mask;
        t2 += t1 >> 51;
        t1 &= Mask;
        t3 += t2 >> 51;
        t2 &= Mask;
        t4 += t3 >> 51;
        t3 &= Mask;
        t0 += 19 * (t4 >> 51);
        t4 &= Mask;
        t1 += t0 >> 51;
        t0 &= Mask;

        // Conditionally subtract p once by adding 19 and checking for a carry out of bit 255.
        var c = (t0 + 19) >> 51;
        c = (t1 + c) >> 51;
        c = (t2 + c) >> 51;
        c = (t3 + c) >> 51;
        c = (t4 + c) >> 51;
        t0 += 19 * c;
        t1 += t0 >> 51;
        t0 &= Mask;
        t2 += t1 >> 51;
        t1 &= Mask;
        t3 += t2 >> 51;
        t2 &= Mask;
        t4 += t3 >> 51;
        t3 &= Mask;
        t4 &= Mask;

        destination[..32].Clear();
        WriteBits(destination, 0, t0, 51);
        WriteBits(destination, 51, t1, 51);
        WriteBits(destination, 102, t2, 51);
        WriteBits(destination, 153, t3, 51);
        WriteBits(destination, 204, t4, 51);
    }

    /// <summary>Returns 1 when the canonical encoding is odd (its low bit is set); otherwise 0.</summary>
    public readonly ulong IsNegative()
    {
        Span<byte> bytes = stackalloc byte[32];
        ToBytes(bytes);
        return (ulong)(bytes[0] & 1);
    }

    /// <summary>Returns 1 when the value is non-zero; otherwise 0.</summary>
    public readonly ulong IsNonZero()
    {
        Span<byte> bytes = stackalloc byte[32];
        ToBytes(bytes);
        byte acc = 0;
        foreach (var b in bytes)
        {
            acc |= b;
        }

        return acc == 0 ? 0UL : 1UL;
    }

    private static ulong ReadBits(ReadOnlySpan<byte> buffer, int bitPos, int count)
    {
        ulong value = 0;
        for (var k = 0; k < count; k++) // fixed count; no data-dependent branch
        {
            var at = bitPos + k;
            var bit = (ulong)((buffer[at >> 3] >> (at & 7)) & 1);
            value |= bit << k;
        }

        return value;
    }

    private static void WriteBits(Span<byte> buffer, int bitPos, ulong value, int count)
    {
        for (var k = 0; k < count; k++) // fixed count; no data-dependent branch
        {
            var at = bitPos + k;
            buffer[at >> 3] |= (byte)(((value >> k) & 1) << (at & 7));
        }
    }
}
