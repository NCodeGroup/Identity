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

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NCode.Identity.Jose.EdDsa.Math;

/// <summary>
/// Element of the prime field <c>GF(2^448 - 2^224 - 1)</c> (the edwards448 "golden-ratio" prime) represented as eight
/// 56-bit limbs (radix 2^56) stored in <see cref="ulong"/>s, with <see cref="System.UInt128"/> used for products. All
/// operations are branch-free with no data-dependent memory access, so they are constant-time with respect to the
/// field value. Because <c>2^224 = 2^(56*4)</c> is limb-aligned, the reduction <c>2^448 ≡ 2^224 + 1</c> folds whole
/// limbs. Clean-room implementation of the field defined by RFC 8032 §5.2.
/// </summary>
internal struct Fe448
{
    private const ulong Mask = (1UL << 56) - 1;

    // Limbs of 2*p, each at least 2^56, so a - b never underflows: 2*p = [2M, 2M, 2M, 2M, 2M-2, 2M, 2M, 2M],
    // where M = 2^56 - 1 and the limb-4 adjustment accounts for the -2^224 term.
    private const ulong TwoM = (1UL << 57) - 2; // 2 * (2^56 - 1)
    private const ulong TwoMMinus2 = (1UL << 57) - 4; // 2 * (2^56 - 1) - 2

    private const MethodImplOptions Opt =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    private ulong _l0;
    private ulong _l1;
    private ulong _l2;
    private ulong _l3;
    private ulong _l4;
    private ulong _l5;
    private ulong _l6;
    private ulong _l7;

    public static Fe448 Zero => default;

    public static Fe448 One => new() { _l0 = 1 };

    [MethodImpl(Opt)]
    public static Fe448 FromUInt64(ulong value) => new() { _l0 = value };

    [MethodImpl(Opt)]
    public static Fe448 Add(in Fe448 a, in Fe448 b)
    {
        var r = new Fe448
        {
            _l0 = a._l0 + b._l0,
            _l1 = a._l1 + b._l1,
            _l2 = a._l2 + b._l2,
            _l3 = a._l3 + b._l3,
            _l4 = a._l4 + b._l4,
            _l5 = a._l5 + b._l5,
            _l6 = a._l6 + b._l6,
            _l7 = a._l7 + b._l7,
        };
        r.WeakReduce();
        return r;
    }

    [MethodImpl(Opt)]
    public static Fe448 Sub(in Fe448 a, in Fe448 b)
    {
        var r = new Fe448
        {
            _l0 = a._l0 + TwoM - b._l0,
            _l1 = a._l1 + TwoM - b._l1,
            _l2 = a._l2 + TwoM - b._l2,
            _l3 = a._l3 + TwoM - b._l3,
            _l4 = a._l4 + TwoMMinus2 - b._l4,
            _l5 = a._l5 + TwoM - b._l5,
            _l6 = a._l6 + TwoM - b._l6,
            _l7 = a._l7 + TwoM - b._l7,
        };
        r.WeakReduce();
        return r;
    }

    [MethodImpl(Opt)]
    public static Fe448 Neg(in Fe448 a) => Sub(Zero, a);

    // Zero-copy views of the eight sequential ulong limb fields as a span (C# structs default to sequential layout,
    // so the limbs are contiguous). Avoids copying limbs in and out of the field element.
    [MethodImpl(Opt)]
    private static Span<ulong> LimbsOf(ref Fe448 value) =>
        MemoryMarshal.CreateSpan(ref Unsafe.As<Fe448, ulong>(ref value), 8);

    [MethodImpl(Opt)]
    private static ReadOnlySpan<ulong> ReadOnlyLimbsOf(in Fe448 value) =>
        MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<Fe448, ulong>(ref Unsafe.AsRef(in value)),
            8
        );

    [MethodImpl(Opt)]
    public static Fe448 Mul(in Fe448 a, in Fe448 b)
    {
        var al = ReadOnlyLimbsOf(a);
        var bl = ReadOnlyLimbsOf(b);

        // Schoolbook product into 15 limbs.
        Span<UInt128> t = stackalloc UInt128[15];
        t.Clear();
        for (var i = 0; i < 8; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                t[i + j] += (UInt128)al[i] * bl[j];
            }
        }

        return Reduce(t);
    }

    [MethodImpl(Opt)]
    public static Fe448 Sqr(in Fe448 a) => Mul(a, a);

    // Folds the 15-limb product using 2^448 ≡ 2^224 + 1 (limb i -> limbs i-8 and i-4), then carry-propagates.
    [MethodImpl(Opt)]
    private static Fe448 Reduce(Span<UInt128> t)
    {
        for (var i = 14; i >= 8; i--)
        {
            t[i - 8] += t[i];
            t[i - 4] += t[i];
        }

        Fe448 result = default;
        var o = LimbsOf(ref result);
        UInt128 carry = 0;
        for (var i = 0; i < 8; i++)
        {
            t[i] += carry;
            o[i] = (ulong)t[i] & Mask;
            carry = t[i] >> 56;
        }

        // Fold the overflow beyond 2^448 back in, then a second (small) carry pass.
        var c = (ulong)carry;
        o[0] += c;
        o[4] += c;

        ulong c2 = 0;
        for (var i = 0; i < 8; i++)
        {
            var v = o[i] + c2;
            o[i] = v & Mask;
            c2 = v >> 56;
        }

        o[0] += c2;
        o[4] += c2;

        return result;
    }

    // Carry-propagates with the golden fold until every limb is below 2^56 (value correct modulo p, not canonical).
    [MethodImpl(Opt)]
    private void WeakReduce()
    {
        var o = LimbsOf(ref this);
        for (var pass = 0; pass < 2; pass++)
        {
            ulong c = 0;
            for (var i = 0; i < 8; i++)
            {
                var v = o[i] + c;
                o[i] = v & Mask;
                c = v >> 56;
            }

            o[0] += c;
            o[4] += c;
        }
    }

    public static Fe448 Invert(in Fe448 a) => Pow(a, PMinus2BitsBE);

    /// <summary>Computes <c>a^((p+1)/4)</c>, the principal square root candidate (p ≡ 3 mod 4).</summary>
    public static Fe448 Sqrt(in Fe448 a) => Pow(a, PPlus1Div4BitsBE);

    private static Fe448 Pow(in Fe448 a, ReadOnlySpan<byte> exponentBigEndian)
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

    [MethodImpl(Opt)]
    public static void CMov(ref Fe448 r, in Fe448 a, ulong move)
    {
        var mask = 0UL - move;
        r._l0 ^= mask & (r._l0 ^ a._l0);
        r._l1 ^= mask & (r._l1 ^ a._l1);
        r._l2 ^= mask & (r._l2 ^ a._l2);
        r._l3 ^= mask & (r._l3 ^ a._l3);
        r._l4 ^= mask & (r._l4 ^ a._l4);
        r._l5 ^= mask & (r._l5 ^ a._l5);
        r._l6 ^= mask & (r._l6 ^ a._l6);
        r._l7 ^= mask & (r._l7 ^ a._l7);
    }

    /// <summary>Loads a field element from 56 little-endian bytes (eight 7-byte limbs).</summary>
    public static Fe448 FromBytes(ReadOnlySpan<byte> source)
    {
        // Limbs 0..6 overlap-read a full 64-bit word and mask to 56 bits; the final limb has only 7 bytes left.
        return new Fe448
        {
            _l0 = BinaryPrimitives.ReadUInt64LittleEndian(source) & Mask,
            _l1 = BinaryPrimitives.ReadUInt64LittleEndian(source[7..]) & Mask,
            _l2 = BinaryPrimitives.ReadUInt64LittleEndian(source[14..]) & Mask,
            _l3 = BinaryPrimitives.ReadUInt64LittleEndian(source[21..]) & Mask,
            _l4 = BinaryPrimitives.ReadUInt64LittleEndian(source[28..]) & Mask,
            _l5 = BinaryPrimitives.ReadUInt64LittleEndian(source[35..]) & Mask,
            _l6 = BinaryPrimitives.ReadUInt64LittleEndian(source[42..]) & Mask,
            _l7 = ReadLimb(source, 49),
        };
    }

    /// <summary>Writes the canonical (reduced, in <c>[0, p)</c>) 56-byte little-endian encoding (eight 7-byte limbs).</summary>
    public readonly void ToBytes(Span<byte> destination)
    {
        Span<ulong> o = stackalloc ulong[8];
        ReadOnlyLimbsOf(this).CopyTo(o);

        // Full carry to limbs < 2^56.
        for (var pass = 0; pass < 2; pass++)
        {
            ulong c = 0;
            for (var i = 0; i < 8; i++)
            {
                var v = o[i] + c;
                o[i] = v & Mask;
                c = v >> 56;
            }

            o[0] += c;
            o[4] += c;
        }

        // Conditionally subtract p (twice) to reach the canonical representative.
        SubtractPConditionally(o);
        SubtractPConditionally(o);

        destination[..56].Clear();
        WriteLimb(destination, 0, o[0]);
        WriteLimb(destination, 7, o[1]);
        WriteLimb(destination, 14, o[2]);
        WriteLimb(destination, 21, o[3]);
        WriteLimb(destination, 28, o[4]);
        WriteLimb(destination, 35, o[5]);
        WriteLimb(destination, 42, o[6]);
        WriteLimb(destination, 49, o[7]);
    }

    // p = [M, M, M, M, M-1, M, M, M]; subtract when the value is >= p, constant-time.
    [MethodImpl(Opt)]
    private static void SubtractPConditionally(Span<ulong> o)
    {
        Span<ulong> p = stackalloc ulong[8];
        p[0] = Mask;
        p[1] = Mask;
        p[2] = Mask;
        p[3] = Mask;
        p[4] = Mask - 1;
        p[5] = Mask;
        p[6] = Mask;
        p[7] = Mask;

        Span<ulong> diff = stackalloc ulong[8];
        ulong borrow = 0;
        for (var i = 0; i < 8; i++)
        {
            var t = (UInt128)o[i] - p[i] - borrow;
            diff[i] = (ulong)t & Mask;
            borrow = (ulong)(t >> 64) & 1;
        }

        // borrow == 0 means o >= p, so select the difference.
        var mask = borrow - 1;
        for (var i = 0; i < 8; i++)
        {
            o[i] ^= mask & (o[i] ^ diff[i]);
        }
    }

    /// <summary>Returns 1 when the canonical encoding is odd; otherwise 0.</summary>
    public readonly ulong IsNegative()
    {
        Span<byte> bytes = stackalloc byte[56];
        ToBytes(bytes);
        return (ulong)(bytes[0] & 1);
    }

    /// <summary>Returns 1 when the value is non-zero; otherwise 0.</summary>
    public readonly ulong IsNonZero()
    {
        Span<byte> bytes = stackalloc byte[56];
        ToBytes(bytes);
        byte acc = 0;
        foreach (var b in bytes)
        {
            acc |= b;
        }

        return acc == 0 ? 0UL : 1UL;
    }

    [MethodImpl(Opt)]
    private static ulong ReadLimb(ReadOnlySpan<byte> source, int offset)
    {
        ulong v = 0;
        for (var k = 0; k < 7; k++)
        {
            v |= (ulong)source[offset + k] << (8 * k);
        }

        return v;
    }

    [MethodImpl(Opt)]
    private static void WriteLimb(Span<byte> destination, int offset, ulong value)
    {
        for (var k = 0; k < 7; k++)
        {
            destination[offset + k] = (byte)(value >> (8 * k));
        }
    }

    // p - 2 = 2^448 - 2^224 - 3, big-endian.
    private static ReadOnlySpan<byte> PMinus2BitsBE =>
        [
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
            0xFE,
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

    // (p + 1) / 4 = 2^446 - 2^222, big-endian.
    private static ReadOnlySpan<byte> PPlus1Div4BitsBE =>
        [
            0x3F,
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
            0xC0,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
        ];
}
