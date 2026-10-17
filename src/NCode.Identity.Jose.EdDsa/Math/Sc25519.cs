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
/// Arithmetic on scalars modulo the Ed25519 group order
/// <c>L = 2^252 + 27742317777372353535851937790883648493</c>. Inputs and outputs are 32-byte little-endian
/// encodings. Reduction is a branch-free, fixed-iteration bit-serial Barrett-free algorithm (Horner evaluation with a
/// constant-time conditional subtraction of <c>L</c> after each step), so it is constant-time with respect to the
/// secret scalar. Clean-room implementation of the scalar field defined by RFC 8032 §5.1.
/// </summary>
internal static class Sc25519
{
    // L as four 64-bit little-endian limbs.
    private const ulong L0 = 0x5812631A5CF5D3EDUL;
    private const ulong L1 = 0x14DEF9DEA2F79CD6UL;
    private const ulong L2 = 0x0000000000000000UL;
    private const ulong L3 = 0x1000000000000000UL;

    private const MethodImplOptions Opt =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    /// <summary>Reduces a 64-byte little-endian value modulo <c>L</c> into a 32-byte little-endian scalar.</summary>
    public static void Reduce(ReadOnlySpan<byte> wide, Span<byte> result)
    {
        Span<ulong> r = stackalloc ulong[4];
        if (BitConverter.IsLittleEndian)
        {
            // Zero-copy reinterpret of the input bytes as eight 64-bit limbs.
            ReduceWide(MemoryMarshal.Cast<byte, ulong>(wide), r);
        }
        else
        {
            Span<ulong> product = stackalloc ulong[8];
            LoadLittleEndian(wide, product);
            ReduceWide(product, r);
        }

        StoreLittleEndian(r, result);
    }

    /// <summary>Computes <c>(a * b + c) mod L</c> for 32-byte little-endian scalars.</summary>
    public static void MulAdd(
        ReadOnlySpan<byte> a,
        ReadOnlySpan<byte> b,
        ReadOnlySpan<byte> c,
        Span<byte> result
    )
    {
        Span<ulong> al = stackalloc ulong[4];
        Span<ulong> bl = stackalloc ulong[4];
        Span<ulong> cl = stackalloc ulong[4];
        LoadLittleEndian(a, al);
        LoadLittleEndian(b, bl);
        LoadLittleEndian(c, cl);

        // product = a * b (eight 64-bit limbs), then add c.
        Span<ulong> product = stackalloc ulong[8];
        product.Clear();
        for (var i = 0; i < 4; i++)
        {
            ulong carry = 0;
            for (var j = 0; j < 4; j++)
            {
                var t = (UInt128)al[i] * bl[j] + product[i + j] + carry;
                product[i + j] = (ulong)t;
                carry = (ulong)(t >> 64);
            }

            product[i + 4] += carry;
        }

        // Add c (four limbs) into the low half of the product with carry propagation.
        ulong addCarry = 0;
        for (var i = 0; i < 4; i++)
        {
            var t = (UInt128)product[i] + cl[i] + addCarry;
            product[i] = (ulong)t;
            addCarry = (ulong)(t >> 64);
        }

        for (var i = 4; i < 8 && addCarry != 0; i++)
        {
            var t = (UInt128)product[i] + addCarry;
            product[i] = (ulong)t;
            addCarry = (ulong)(t >> 64);
        }

        Span<ulong> r = stackalloc ulong[4];
        ReduceWide(product, r);
        StoreLittleEndian(r, result);
    }

    /// <summary>Returns <c>true</c> when the 32-byte little-endian scalar is canonically reduced (strictly below
    /// <c>L</c>). Used to reject malleable/non-canonical signature <c>S</c> values; operates on public data.</summary>
    [MethodImpl(Opt)]
    public static bool IsCanonical(ReadOnlySpan<byte> scalar)
    {
        Span<ulong> r = stackalloc ulong[4];
        LoadLittleEndian(scalar, r);
        if (r[3] != L3)
        {
            return r[3] < L3;
        }

        if (r[2] != L2)
        {
            return r[2] < L2;
        }

        if (r[1] != L1)
        {
            return r[1] < L1;
        }

        return r[0] < L0;
    }

    // Horner reduction: fold the 512-bit value bit-by-bit (MSB first) into an accumulator kept below L via a
    // constant-time conditional subtraction after each doubling. Fixed 512 iterations, no data-dependent branches.
    private static void ReduceWide(ReadOnlySpan<ulong> product, Span<ulong> r)
    {
        r.Clear();
        for (var word = 7; word >= 0; word--)
        {
            var limb = product[word];
            for (var bit = 63; bit >= 0; bit--)
            {
                var nextBit = (limb >> bit) & 1;
                ShiftLeftOneAddBit(r, nextBit);
                ConditionalSubtractL(r);
            }
        }
    }

    [MethodImpl(Opt)]
    private static void ShiftLeftOneAddBit(Span<ulong> r, ulong bit)
    {
        // Unrolled 4-limb left shift by one with the incoming bit folded into limb 0.
        ulong r0 = r[0],
            r1 = r[1],
            r2 = r[2],
            r3 = r[3];
        r[0] = (r0 << 1) | bit;
        r[1] = (r1 << 1) | (r0 >> 63);
        r[2] = (r2 << 1) | (r1 >> 63);
        r[3] = (r3 << 1) | (r2 >> 63);
        // The top carry (r3 >> 63) is discarded; the accumulator stays below 2L < 2^253.
    }

    // Subtracts L when r >= L, constant-time (selects r - L vs r via a borrow-derived mask).
    [MethodImpl(Opt)]
    private static void ConditionalSubtractL(Span<ulong> r)
    {
        Span<ulong> diff = stackalloc ulong[4];
        ulong borrow = 0;
        borrow = SubLimb(r[0], L0, borrow, out diff[0]);
        borrow = SubLimb(r[1], L1, borrow, out diff[1]);
        borrow = SubLimb(r[2], L2, borrow, out diff[2]);
        borrow = SubLimb(r[3], L3, borrow, out diff[3]);

        // borrow == 0 means r >= L, so select the difference; otherwise keep r.
        var mask = borrow - 1; // 0xFFFF... when borrow==0, 0 when borrow==1
        r[0] ^= mask & (r[0] ^ diff[0]);
        r[1] ^= mask & (r[1] ^ diff[1]);
        r[2] ^= mask & (r[2] ^ diff[2]);
        r[3] ^= mask & (r[3] ^ diff[3]);
    }

    [MethodImpl(Opt)]
    private static ulong SubLimb(ulong a, ulong b, ulong borrowIn, out ulong result)
    {
        var t = (UInt128)a - b - borrowIn;
        result = (ulong)t;
        return (ulong)(t >> 64) & 1; // borrow out
    }

    [MethodImpl(Opt)]
    private static void LoadLittleEndian(ReadOnlySpan<byte> bytes, Span<ulong> limbs)
    {
        var src = MemoryMarshal.Cast<byte, ulong>(bytes);
        if (BitConverter.IsLittleEndian)
        {
            src.CopyTo(limbs);
        }
        else
        {
            for (var i = 0; i < limbs.Length; i++)
            {
                limbs[i] = BinaryPrimitives.ReverseEndianness(src[i]);
            }
        }
    }

    [MethodImpl(Opt)]
    private static void StoreLittleEndian(ReadOnlySpan<ulong> limbs, Span<byte> bytes)
    {
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.AsBytes(limbs).CopyTo(bytes);
        }
        else
        {
            for (var i = 0; i < limbs.Length; i++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(i * 8, 8), limbs[i]);
            }
        }
    }
}
