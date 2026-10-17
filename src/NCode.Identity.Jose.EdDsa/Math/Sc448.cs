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

namespace NCode.Identity.Jose.EdDsa.Math;

/// <summary>
/// Arithmetic on scalars modulo the Ed448 group order
/// <c>L = 2^446 - 13818066809895115352007386748515426880336692474882178609894547503885</c>. Scalars are 57-byte
/// little-endian encodings. Reduction is a branch-free, fixed-iteration bit-serial Horner algorithm with a
/// constant-time conditional subtraction of <c>L</c>, so it is constant-time with respect to the secret scalar.
/// Clean-room implementation of the scalar field defined by RFC 8032 §5.2.
/// </summary>
internal static class Sc448
{
    public const int ScalarSize = 57;

    // L as eight 64-bit little-endian limbs (limb 7 is zero; L occupies 446 bits).
    private const ulong L0 = 0x2378C292AB5844F3UL;
    private const ulong L1 = 0x216CC2728DC58F55UL;
    private const ulong L2 = 0xC44EDB49AED63690UL;
    private const ulong L3 = 0xFFFFFFFF7CCA23E9UL;
    private const ulong L4 = 0xFFFFFFFFFFFFFFFFUL;
    private const ulong L5 = 0xFFFFFFFFFFFFFFFFUL;
    private const ulong L6 = 0x3FFFFFFFFFFFFFFFUL;
    private const ulong L7 = 0x0000000000000000UL;

    private const MethodImplOptions Opt =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    /// <summary>Reduces a 114-byte little-endian value (the SHAKE256 output) modulo <c>L</c> into a 57-byte scalar.</summary>
    public static void Reduce(ReadOnlySpan<byte> wide, Span<byte> result)
    {
        Span<ulong> r = stackalloc ulong[8];
        r.Clear();
        for (var byteIndex = wide.Length - 1; byteIndex >= 0; byteIndex--)
        {
            var octet = wide[byteIndex];
            for (var bit = 7; bit >= 0; bit--)
            {
                ShiftLeftOneAddBit(r, (ulong)((octet >> bit) & 1));
                ConditionalSubtractL(r);
            }
        }

        WriteScalar(r, result);
    }

    /// <summary>Computes <c>(a * b + c) mod L</c> for 57-byte little-endian scalars.</summary>
    public static void MulAdd(
        ReadOnlySpan<byte> a,
        ReadOnlySpan<byte> b,
        ReadOnlySpan<byte> c,
        Span<byte> result
    )
    {
        Span<ulong> al = stackalloc ulong[8];
        Span<ulong> bl = stackalloc ulong[8];
        Span<ulong> cl = stackalloc ulong[8];
        LoadScalar(a, al);
        LoadScalar(b, bl);
        LoadScalar(c, cl);

        Span<ulong> product = stackalloc ulong[16];
        product.Clear();
        for (var i = 0; i < 8; i++)
        {
            ulong carry = 0;
            for (var j = 0; j < 8; j++)
            {
                var t = (UInt128)al[i] * bl[j] + product[i + j] + carry;
                product[i + j] = (ulong)t;
                carry = (ulong)(t >> 64);
            }

            product[i + 8] += carry;
        }

        ulong addCarry = 0;
        for (var i = 0; i < 8; i++)
        {
            var t = (UInt128)product[i] + cl[i] + addCarry;
            product[i] = (ulong)t;
            addCarry = (ulong)(t >> 64);
        }

        for (var i = 8; i < 16 && addCarry != 0; i++)
        {
            var t = (UInt128)product[i] + addCarry;
            product[i] = (ulong)t;
            addCarry = (ulong)(t >> 64);
        }

        Span<ulong> r = stackalloc ulong[8];
        r.Clear();
        for (var word = 15; word >= 0; word--)
        {
            var limb = product[word];
            for (var bit = 63; bit >= 0; bit--)
            {
                ShiftLeftOneAddBit(r, (limb >> bit) & 1);
                ConditionalSubtractL(r);
            }
        }

        WriteScalar(r, result);
    }

    /// <summary>Returns <c>true</c> when the 57-byte little-endian scalar is canonically reduced (below <c>L</c>).</summary>
    [MethodImpl(Opt)]
    public static bool IsCanonical(ReadOnlySpan<byte> scalar)
    {
        Span<ulong> r = stackalloc ulong[8];
        LoadScalar(scalar, r);
        ReadOnlySpan<ulong> l = [L0, L1, L2, L3, L4, L5, L6, L7];
        for (var i = 7; i >= 0; i--)
        {
            if (r[i] != l[i])
            {
                return r[i] < l[i];
            }
        }

        return false; // equal to L is not canonical
    }

    [MethodImpl(Opt)]
    private static void ShiftLeftOneAddBit(Span<ulong> r, ulong bit)
    {
        ulong r0 = r[0],
            r1 = r[1],
            r2 = r[2],
            r3 = r[3],
            r4 = r[4],
            r5 = r[5],
            r6 = r[6],
            r7 = r[7];
        r[0] = (r0 << 1) | bit;
        r[1] = (r1 << 1) | (r0 >> 63);
        r[2] = (r2 << 1) | (r1 >> 63);
        r[3] = (r3 << 1) | (r2 >> 63);
        r[4] = (r4 << 1) | (r3 >> 63);
        r[5] = (r5 << 1) | (r4 >> 63);
        r[6] = (r6 << 1) | (r5 >> 63);
        r[7] = (r7 << 1) | (r6 >> 63);
    }

    [MethodImpl(Opt)]
    private static void ConditionalSubtractL(Span<ulong> r)
    {
        Span<ulong> diff = stackalloc ulong[8];
        ulong borrow = 0;
        borrow = SubLimb(r[0], L0, borrow, out diff[0]);
        borrow = SubLimb(r[1], L1, borrow, out diff[1]);
        borrow = SubLimb(r[2], L2, borrow, out diff[2]);
        borrow = SubLimb(r[3], L3, borrow, out diff[3]);
        borrow = SubLimb(r[4], L4, borrow, out diff[4]);
        borrow = SubLimb(r[5], L5, borrow, out diff[5]);
        borrow = SubLimb(r[6], L6, borrow, out diff[6]);
        borrow = SubLimb(r[7], L7, borrow, out diff[7]);

        var mask = borrow - 1; // 0xFFFF... when borrow==0 (r >= L), else 0
        r[0] ^= mask & (r[0] ^ diff[0]);
        r[1] ^= mask & (r[1] ^ diff[1]);
        r[2] ^= mask & (r[2] ^ diff[2]);
        r[3] ^= mask & (r[3] ^ diff[3]);
        r[4] ^= mask & (r[4] ^ diff[4]);
        r[5] ^= mask & (r[5] ^ diff[5]);
        r[6] ^= mask & (r[6] ^ diff[6]);
        r[7] ^= mask & (r[7] ^ diff[7]);
    }

    [MethodImpl(Opt)]
    private static ulong SubLimb(ulong a, ulong b, ulong borrowIn, out ulong result)
    {
        var t = (UInt128)a - b - borrowIn;
        result = (ulong)t;
        return (ulong)(t >> 64) & 1;
    }

    // Loads a 57-byte little-endian scalar into eight limbs (limbs 0..6 full, limb 7 holds the final byte).
    [MethodImpl(Opt)]
    private static void LoadScalar(ReadOnlySpan<byte> scalar, Span<ulong> limbs)
    {
        limbs[0] = BinaryPrimitives.ReadUInt64LittleEndian(scalar);
        limbs[1] = BinaryPrimitives.ReadUInt64LittleEndian(scalar[8..]);
        limbs[2] = BinaryPrimitives.ReadUInt64LittleEndian(scalar[16..]);
        limbs[3] = BinaryPrimitives.ReadUInt64LittleEndian(scalar[24..]);
        limbs[4] = BinaryPrimitives.ReadUInt64LittleEndian(scalar[32..]);
        limbs[5] = BinaryPrimitives.ReadUInt64LittleEndian(scalar[40..]);
        limbs[6] = BinaryPrimitives.ReadUInt64LittleEndian(scalar[48..]);
        limbs[7] = scalar[56];
    }

    // Writes a reduced scalar (below L < 2^448) as 57 little-endian bytes; the final byte is always zero.
    [MethodImpl(Opt)]
    private static void WriteScalar(ReadOnlySpan<ulong> limbs, Span<byte> result)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(result, limbs[0]);
        BinaryPrimitives.WriteUInt64LittleEndian(result[8..], limbs[1]);
        BinaryPrimitives.WriteUInt64LittleEndian(result[16..], limbs[2]);
        BinaryPrimitives.WriteUInt64LittleEndian(result[24..], limbs[3]);
        BinaryPrimitives.WriteUInt64LittleEndian(result[32..], limbs[4]);
        BinaryPrimitives.WriteUInt64LittleEndian(result[40..], limbs[5]);
        BinaryPrimitives.WriteUInt64LittleEndian(result[48..], limbs[6]);
        result[56] = (byte)limbs[7];
    }
}
