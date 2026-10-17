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
/// Points on the edwards448 Edwards curve <c>x^2 + y^2 = 1 + d*x^2*y^2</c> (untwisted, <c>a = 1</c>, <c>d = -39081</c>)
/// in extended homogeneous coordinates <c>(X : Y : Z : T)</c>. Scalar multiplication is constant-time
/// (double-and-add-always with masked select); point decoding operates only on public values and may branch.
/// Clean-room implementation of the group defined by RFC 8032 §5.2.
/// </summary>
internal struct Ge448
{
    public const int EncodedSize = 57;

    private const MethodImplOptions Opt =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    private Fe448 _x;
    private Fe448 _y;
    private Fe448 _z;
    private Fe448 _t;

    // Curve constant d = -39081.
    private static readonly Fe448 D = Fe448.Neg(Fe448.FromUInt64(39081));

    public static Ge448 Identity =>
        new()
        {
            _x = Fe448.Zero,
            _y = Fe448.One,
            _z = Fe448.One,
            _t = Fe448.Zero,
        };

    /// <summary>The edwards448 base point B (generator), per RFC 8032 §5.2.</summary>
    public static readonly Ge448 Base = ComputeBase();

    // Compressed base-point encoding (little-endian y with x even), RFC 8032 §5.2.
    private static ReadOnlySpan<byte> BaseEncoding =>
        [
            0x14,
            0xFA,
            0x30,
            0xF2,
            0x5B,
            0x79,
            0x08,
            0x98,
            0xAD,
            0xC8,
            0xD7,
            0x4E,
            0x2C,
            0x13,
            0xBD,
            0xFD,
            0xC4,
            0x39,
            0x7C,
            0xE6,
            0x1C,
            0xFF,
            0xD3,
            0x3A,
            0xD7,
            0xC2,
            0xA0,
            0x05,
            0x1E,
            0x9C,
            0x78,
            0x87,
            0x40,
            0x98,
            0xA3,
            0x6C,
            0x73,
            0x73,
            0xEA,
            0x4B,
            0x62,
            0xC7,
            0xC9,
            0x56,
            0x37,
            0x20,
            0x76,
            0x88,
            0x24,
            0xBC,
            0xB6,
            0x6E,
            0x71,
            0x46,
            0x3F,
            0x69,
            0x00,
        ];

    private static Ge448 ComputeBase()
    {
        if (!TryFromBytes(BaseEncoding, out var point))
        {
            throw new InvalidOperationException("Failed to decode the Ed448 base point.");
        }

        return point;
    }

    // Unified addition for a = 1 (add-2008-hwcd), using d (not 2d) and Z1*Z2 (not doubled).
    [MethodImpl(Opt)]
    public static Ge448 Add(in Ge448 p, in Ge448 q)
    {
        var a = Fe448.Mul(p._x, q._x);
        var b = Fe448.Mul(p._y, q._y);
        var c = Fe448.Mul(Fe448.Mul(D, p._t), q._t);
        var d = Fe448.Mul(p._z, q._z);
        var e = Fe448.Sub(Fe448.Sub(Fe448.Mul(Fe448.Add(p._x, p._y), Fe448.Add(q._x, q._y)), a), b);
        var f = Fe448.Sub(d, c);
        var g = Fe448.Add(d, c);
        var h = Fe448.Sub(b, a);
        return new Ge448
        {
            _x = Fe448.Mul(e, f),
            _y = Fe448.Mul(g, h),
            _t = Fe448.Mul(e, h),
            _z = Fe448.Mul(f, g),
        };
    }

    // Doubling for a = 1 (dbl-2008-hwcd).
    [MethodImpl(Opt)]
    public static Ge448 Dbl(in Ge448 p)
    {
        var a = Fe448.Sqr(p._x);
        var b = Fe448.Sqr(p._y);
        var c = Fe448.Add(Fe448.Sqr(p._z), Fe448.Sqr(p._z));
        var e = Fe448.Sub(Fe448.Sub(Fe448.Sqr(Fe448.Add(p._x, p._y)), a), b);
        var g = Fe448.Add(a, b);
        var f = Fe448.Sub(g, c);
        var h = Fe448.Sub(a, b);
        return new Ge448
        {
            _x = Fe448.Mul(e, f),
            _y = Fe448.Mul(g, h),
            _t = Fe448.Mul(e, h),
            _z = Fe448.Mul(f, g),
        };
    }

    [MethodImpl(Opt)]
    public static Ge448 Neg(in Ge448 p) =>
        new()
        {
            _x = Fe448.Neg(p._x),
            _y = p._y,
            _z = p._z,
            _t = Fe448.Neg(p._t),
        };

    [MethodImpl(Opt)]
    public static void CMov(ref Ge448 r, in Ge448 a, ulong move)
    {
        Fe448.CMov(ref r._x, a._x, move);
        Fe448.CMov(ref r._y, a._y, move);
        Fe448.CMov(ref r._z, a._z, move);
        Fe448.CMov(ref r._t, a._t, move);
    }

    /// <summary>Computes <c>[scalar] * point</c> in constant time (the schedule is independent of the scalar).</summary>
    public static Ge448 ScalarMult(ReadOnlySpan<byte> scalar, in Ge448 point)
    {
        var r = Identity;
        var bits = scalar.Length * 8;
        for (var i = bits - 1; i >= 0; i--)
        {
            r = Dbl(r);
            var sum = Add(r, point);
            var bit = (ulong)((scalar[i >> 3] >> (i & 7)) & 1);
            CMov(ref r, sum, bit);
        }

        return r;
    }

    /// <summary>Computes <c>[scalar] * B</c> for the base point in constant time.</summary>
    public static Ge448 ScalarMultBase(ReadOnlySpan<byte> scalar) => ScalarMult(scalar, Base);

    /// <summary>Encodes the point as its 57-byte compressed form (56-byte y plus the sign bit of x in byte 56).</summary>
    public readonly void ToBytes(Span<byte> destination)
    {
        var zInv = Fe448.Invert(_z);
        var x = Fe448.Mul(_x, zInv);
        var y = Fe448.Mul(_y, zInv);
        destination[..EncodedSize].Clear();
        y.ToBytes(destination[..56]);
        destination[56] = (byte)(x.IsNegative() << 7);
    }

    /// <summary>Decodes a 57-byte compressed point, rejecting a non-canonical <c>y</c> or a missing square root.</summary>
    public static bool TryFromBytes(ReadOnlySpan<byte> source, out Ge448 point)
    {
        point = default;
        if (source.Length != EncodedSize || (source[56] & 0x7F) != 0)
        {
            return false; // the unused low bits of the final octet must be zero
        }

        var signBit = (ulong)(source[56] >> 7);
        var y = Fe448.FromBytes(source[..56]);

        Span<byte> reencoded = stackalloc byte[56];
        y.ToBytes(reencoded);
        if (!reencoded.SequenceEqual(source[..56]))
        {
            return false; // non-canonical y
        }

        if (!TryRecoverX(y, signBit, out var x))
        {
            return false;
        }

        point = new Ge448
        {
            _x = x,
            _y = y,
            _z = Fe448.One,
            _t = Fe448.Mul(x, y),
        };
        return true;
    }

    // Recovers x from y and the sign bit per RFC 8032 §5.2.3 (p ≡ 3 mod 4, so there is no sqrt(-1) correction).
    private static bool TryRecoverX(in Fe448 y, ulong signBit, out Fe448 x)
    {
        var y2 = Fe448.Sqr(y);
        var u = Fe448.Sub(y2, Fe448.One);
        var v = Fe448.Sub(Fe448.Mul(D, y2), Fe448.One);
        var candidate = Fe448.Sqrt(Fe448.Mul(u, Fe448.Invert(v)));

        if (Fe448.Sub(Fe448.Mul(v, Fe448.Sqr(candidate)), u).IsNonZero() != 0)
        {
            x = default;
            return false; // not a square: invalid point
        }

        if (candidate.IsNonZero() == 0 && signBit == 1)
        {
            x = default;
            return false;
        }

        if (candidate.IsNegative() != signBit)
        {
            candidate = Fe448.Neg(candidate);
        }

        x = candidate;
        return true;
    }
}
