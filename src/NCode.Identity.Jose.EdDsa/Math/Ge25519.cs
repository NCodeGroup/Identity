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
/// Points on the Ed25519 twisted-Edwards curve <c>-x^2 + y^2 = 1 + d*x^2*y^2</c> in extended homogeneous coordinates
/// <c>(X : Y : Z : T)</c> with <c>x = X/Z</c>, <c>y = Y/Z</c>, <c>x*y = T/Z</c>. Point addition, doubling, and scalar
/// multiplication use branch-free field operations and a constant-time (double-and-add-always, masked select) scalar
/// ladder, so multiplication by a secret scalar is constant-time. Point decoding operates only on public values and
/// may branch. Clean-room implementation of the group defined by RFC 8032 §5.1.
/// </summary>
internal struct Ge25519
{
    private const MethodImplOptions Opt =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    private Fe25519 _x;
    private Fe25519 _y;
    private Fe25519 _z;
    private Fe25519 _t;

    // Curve constant d = -121665 / 121666, and 2*d (used by the addition formula). Computed once at type load.
    private static readonly Fe25519 D = Fe25519.Mul(
        Fe25519.Neg(Fe25519.FromUInt64(121665)),
        Fe25519.Invert(Fe25519.FromUInt64(121666))
    );

    private static readonly Fe25519 D2 = Fe25519.Add(D, D);

    public static Ge25519 Identity =>
        new()
        {
            _x = Fe25519.Zero,
            _y = Fe25519.One,
            _z = Fe25519.One,
            _t = Fe25519.Zero,
        };

    /// <summary>The Ed25519 base point B (generator), with <c>y = 4/5</c> and the even <c>x</c>.</summary>
    public static readonly Ge25519 Base = ComputeBase();

    private static Ge25519 ComputeBase()
    {
        var y = Fe25519.Mul(Fe25519.FromUInt64(4), Fe25519.Invert(Fe25519.FromUInt64(5)));
        if (!TryRecoverX(y, 0, out var x))
        {
            throw new InvalidOperationException("Failed to derive the Ed25519 base point.");
        }

        return new Ge25519
        {
            _x = x,
            _y = y,
            _z = Fe25519.One,
            _t = Fe25519.Mul(x, y),
        };
    }

    [MethodImpl(Opt)]
    public static Ge25519 Add(in Ge25519 p, in Ge25519 q)
    {
        var a = Fe25519.Mul(Fe25519.Sub(p._y, p._x), Fe25519.Sub(q._y, q._x));
        var b = Fe25519.Mul(Fe25519.Add(p._y, p._x), Fe25519.Add(q._y, q._x));
        var c = Fe25519.Mul(Fe25519.Mul(p._t, D2), q._t);
        var d = Fe25519.Mul(Fe25519.Add(p._z, p._z), q._z);
        var e = Fe25519.Sub(b, a);
        var f = Fe25519.Sub(d, c);
        var g = Fe25519.Add(d, c);
        var h = Fe25519.Add(b, a);
        return new Ge25519
        {
            _x = Fe25519.Mul(e, f),
            _y = Fe25519.Mul(g, h),
            _t = Fe25519.Mul(e, h),
            _z = Fe25519.Mul(f, g),
        };
    }

    [MethodImpl(Opt)]
    public static Ge25519 Dbl(in Ge25519 p)
    {
        var a = Fe25519.Sqr(p._x);
        var b = Fe25519.Sqr(p._y);
        var c = Fe25519.Add(Fe25519.Sqr(p._z), Fe25519.Sqr(p._z));
        var e = Fe25519.Sub(Fe25519.Sub(Fe25519.Sqr(Fe25519.Add(p._x, p._y)), a), b);
        var g = Fe25519.Sub(b, a);
        var f = Fe25519.Sub(g, c);
        var h = Fe25519.Neg(Fe25519.Add(a, b));
        return new Ge25519
        {
            _x = Fe25519.Mul(e, f),
            _y = Fe25519.Mul(g, h),
            _t = Fe25519.Mul(e, h),
            _z = Fe25519.Mul(f, g),
        };
    }

    [MethodImpl(Opt)]
    public static Ge25519 Neg(in Ge25519 p) =>
        new()
        {
            _x = Fe25519.Neg(p._x),
            _y = p._y,
            _z = p._z,
            _t = Fe25519.Neg(p._t),
        };

    [MethodImpl(Opt)]
    public static void CMov(ref Ge25519 r, in Ge25519 a, ulong move)
    {
        Fe25519.CMov(ref r._x, a._x, move);
        Fe25519.CMov(ref r._y, a._y, move);
        Fe25519.CMov(ref r._z, a._z, move);
        Fe25519.CMov(ref r._t, a._t, move);
    }

    /// <summary>Computes <c>[scalar] * point</c> in constant time (the schedule is independent of the scalar).</summary>
    public static Ge25519 ScalarMult(ReadOnlySpan<byte> scalar, in Ge25519 point)
    {
        var r = Identity;
        for (var i = 255; i >= 0; i--)
        {
            r = Dbl(r);
            var sum = Add(r, point);
            var bit = (ulong)((scalar[i >> 3] >> (i & 7)) & 1);
            CMov(ref r, sum, bit);
        }

        return r;
    }

    /// <summary>Computes <c>[scalar] * B</c> for the base point in constant time.</summary>
    public static Ge25519 ScalarMultBase(ReadOnlySpan<byte> scalar) => ScalarMult(scalar, Base);

    /// <summary>Encodes the point as its 32-byte little-endian compressed form (y with the sign bit of x).</summary>
    public readonly void ToBytes(Span<byte> destination)
    {
        var zInv = Fe25519.Invert(_z);
        var x = Fe25519.Mul(_x, zInv);
        var y = Fe25519.Mul(_y, zInv);
        y.ToBytes(destination);
        destination[31] |= (byte)(x.IsNegative() << 7);
    }

    /// <summary>Decodes a 32-byte compressed point, rejecting a non-canonical <c>y</c> or a missing square root.</summary>
    public static bool TryFromBytes(ReadOnlySpan<byte> source, out Ge25519 point)
    {
        point = default;
        var signBit = (ulong)(source[31] >> 7);
        var y = Fe25519.FromBytes(source);

        // Reject a non-canonical y encoding (y must be the reduced representative in [0, p)).
        Span<byte> reencoded = stackalloc byte[32];
        y.ToBytes(reencoded);
        Span<byte> clean = stackalloc byte[32];
        source[..32].CopyTo(clean);
        clean[31] &= 0x7F;
        if (!reencoded.SequenceEqual(clean))
        {
            return false;
        }

        if (!TryRecoverX(y, signBit, out var x))
        {
            return false;
        }

        point = new Ge25519
        {
            _x = x,
            _y = y,
            _z = Fe25519.One,
            _t = Fe25519.Mul(x, y),
        };
        return true;
    }

    // Recovers x from y and the desired sign bit per RFC 8032 §5.1.3. Operates on public values, so it may branch.
    private static bool TryRecoverX(in Fe25519 y, ulong signBit, out Fe25519 x)
    {
        var y2 = Fe25519.Sqr(y);
        var u = Fe25519.Sub(y2, Fe25519.One);
        var v = Fe25519.Add(Fe25519.Mul(D, y2), Fe25519.One);

        var v2 = Fe25519.Sqr(v);
        var v3 = Fe25519.Mul(v2, v);
        var v7 = Fe25519.Mul(Fe25519.Sqr(v3), v);
        var pow = Fe25519.PowPMinus5Div8(Fe25519.Mul(u, v7));
        var candidate = Fe25519.Mul(Fe25519.Mul(u, v3), pow);

        var vxx = Fe25519.Mul(v, Fe25519.Sqr(candidate));
        if (Fe25519.Sub(vxx, u).IsNonZero() == 0)
        {
            // vxx == u: candidate is already a root.
        }
        else if (Fe25519.Add(vxx, u).IsNonZero() == 0)
        {
            // vxx == -u: multiply by sqrt(-1).
            candidate = Fe25519.Mul(candidate, Fe25519.SqrtMinusOne);
        }
        else
        {
            x = default;
            return false;
        }

        // x == 0 is only valid when the requested sign is positive (bit 0).
        if (candidate.IsNonZero() == 0 && signBit == 1)
        {
            x = default;
            return false;
        }

        if (candidate.IsNegative() != signBit)
        {
            candidate = Fe25519.Neg(candidate);
        }

        x = candidate;
        return true;
    }
}
