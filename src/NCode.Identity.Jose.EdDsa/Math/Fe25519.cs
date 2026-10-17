#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
    // these methods branch on or index by field values. See ADR-0116 / the crypto-math performance convention.
    private const MethodImplOptions Opt =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    private ulong _l0;
    private ulong _l1;
    private ulong _l2;
    private ulong _l3;
    private ulong _l4;

    public static Fe25519 Zero => default;

    public static Fe25519 One => new() { _l0 = 1 };

    /// <summary>Creates a field element from a small unsigned integer (the value must be below 2^51).</summary>
    [MethodImpl(Opt)]
    public static Fe25519 FromUInt64(ulong value) => new() { _l0 = value };

    /// <summary>A fixed square root of <c>-1</c> modulo <c>p</c>, used to correct the sign during point decompression.</summary>
    public static readonly Fe25519 SqrtMinusOne = Pow(FromUInt64(2), PMinus1Div4BitsBE);

    [MethodImpl(Opt)]
    public static Fe25519 Add(in Fe25519 a, in Fe25519 b)
    {
        var r = new Fe25519
        {
            _l0 = a._l0 + b._l0,
            _l1 = a._l1 + b._l1,
            _l2 = a._l2 + b._l2,
            _l3 = a._l3 + b._l3,
            _l4 = a._l4 + b._l4,
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
            _l0 = a._l0 + 0x0F_FFFF_FFFF_FFDAUL - b._l0,
            _l1 = a._l1 + 0x0F_FFFF_FFFF_FFFEUL - b._l1,
            _l2 = a._l2 + 0x0F_FFFF_FFFF_FFFEUL - b._l2,
            _l3 = a._l3 + 0x0F_FFFF_FFFF_FFFEUL - b._l3,
            _l4 = a._l4 + 0x0F_FFFF_FFFF_FFFEUL - b._l4,
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
        ulong b1_19 = 19 * b._l1;
        ulong b2_19 = 19 * b._l2;
        ulong b3_19 = 19 * b._l3;
        ulong b4_19 = 19 * b._l4;

        UInt128 t0 =
            (UInt128)a._l0 * b._l0
            + (UInt128)a._l1 * b4_19
            + (UInt128)a._l2 * b3_19
            + (UInt128)a._l3 * b2_19
            + (UInt128)a._l4 * b1_19;
        UInt128 t1 =
            (UInt128)a._l0 * b._l1
            + (UInt128)a._l1 * b._l0
            + (UInt128)a._l2 * b4_19
            + (UInt128)a._l3 * b3_19
            + (UInt128)a._l4 * b2_19;
        UInt128 t2 =
            (UInt128)a._l0 * b._l2
            + (UInt128)a._l1 * b._l1
            + (UInt128)a._l2 * b._l0
            + (UInt128)a._l3 * b4_19
            + (UInt128)a._l4 * b3_19;
        UInt128 t3 =
            (UInt128)a._l0 * b._l3
            + (UInt128)a._l1 * b._l2
            + (UInt128)a._l2 * b._l1
            + (UInt128)a._l3 * b._l0
            + (UInt128)a._l4 * b4_19;
        UInt128 t4 =
            (UInt128)a._l0 * b._l4
            + (UInt128)a._l1 * b._l3
            + (UInt128)a._l2 * b._l2
            + (UInt128)a._l3 * b._l1
            + (UInt128)a._l4 * b._l0;

        return CarryReduce(t0, t1, t2, t3, t4);
    }

    // Dedicated squaring: the symmetric cross terms are doubled once, saving multiplications versus Mul(a, a).
    [MethodImpl(Opt)]
    public static Fe25519 Sqr(in Fe25519 a)
    {
        ulong a0 = a._l0,
            a1 = a._l1,
            a2 = a._l2,
            a3 = a._l3,
            a4 = a._l4;
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
        UInt128 t0 = (UInt128)a._l0 * 121666;
        UInt128 t1 = (UInt128)a._l1 * 121666;
        UInt128 t2 = (UInt128)a._l2 * 121666;
        UInt128 t3 = (UInt128)a._l3 * 121666;
        UInt128 t4 = (UInt128)a._l4 * 121666;
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
            _l0 = r0,
            _l1 = r1,
            _l2 = r2,
            _l3 = r3,
            _l4 = r4,
        };
    }

    // Single carry-propagation pass; leaves limbs below 2^51 except possibly limb 1 which stays below 2^52.
    [MethodImpl(Opt)]
    private void WeakReduce()
    {
        _l1 += _l0 >> 51;
        _l0 &= Mask;
        _l2 += _l1 >> 51;
        _l1 &= Mask;
        _l3 += _l2 >> 51;
        _l2 &= Mask;
        _l4 += _l3 >> 51;
        _l3 &= Mask;
        _l0 += 19 * (_l4 >> 51);
        _l4 &= Mask;
        _l1 += _l0 >> 51;
        _l0 &= Mask;
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
        r._l0 ^= mask & (r._l0 ^ a._l0);
        r._l1 ^= mask & (r._l1 ^ a._l1);
        r._l2 ^= mask & (r._l2 ^ a._l2);
        r._l3 ^= mask & (r._l3 ^ a._l3);
        r._l4 ^= mask & (r._l4 ^ a._l4);
    }

    /// <summary>Constant-time conditional swap of <paramref name="a"/> and <paramref name="b"/> when
    /// <paramref name="swap"/> is 1.</summary>
    [MethodImpl(Opt)]
    public static void CSwap(ref Fe25519 a, ref Fe25519 b, ulong swap)
    {
        var mask = 0UL - swap;
        var t0 = mask & (a._l0 ^ b._l0);
        a._l0 ^= t0;
        b._l0 ^= t0;
        var t1 = mask & (a._l1 ^ b._l1);
        a._l1 ^= t1;
        b._l1 ^= t1;
        var t2 = mask & (a._l2 ^ b._l2);
        a._l2 ^= t2;
        b._l2 ^= t2;
        var t3 = mask & (a._l3 ^ b._l3);
        a._l3 ^= t3;
        b._l3 ^= t3;
        var t4 = mask & (a._l4 ^ b._l4);
        a._l4 ^= t4;
        b._l4 ^= t4;
    }

    /// <summary>Loads a field element from 32 little-endian bytes, ignoring the most-significant bit (bit 255).</summary>
    public static Fe25519 FromBytes(ReadOnlySpan<byte> source)
    {
        return new Fe25519
        {
            _l0 = ReadBits(source, 0, 51),
            _l1 = ReadBits(source, 51, 51),
            _l2 = ReadBits(source, 102, 51),
            _l3 = ReadBits(source, 153, 51),
            _l4 = ReadBits(source, 204, 51),
        };
    }

    /// <summary>Writes the canonical (fully reduced, in <c>[0, p)</c>) little-endian 32-byte encoding.</summary>
    public readonly void ToBytes(Span<byte> destination)
    {
        ulong t0 = _l0,
            t1 = _l1,
            t2 = _l2,
            t3 = _l3,
            t4 = _l4;

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
