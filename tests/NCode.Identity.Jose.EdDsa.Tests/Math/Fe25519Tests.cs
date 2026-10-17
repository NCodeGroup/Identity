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

using System.Numerics;
using NCode.Identity.Jose.EdDsa.Math;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Validates the constant-time GF(2^255-19) field arithmetic against a BigInteger reference (independent of the
// field implementation) over randomized inputs, plus encode/decode round-trips and algebraic identities.
public class Fe25519Tests
{
    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;

    private const int Iterations = 2000;

    private static Fe25519 ToFe(BigInteger value)
    {
        Span<byte> bytes = stackalloc byte[32];
        WriteLe(value, bytes);
        return Fe25519.FromBytes(bytes);
    }

    private static BigInteger ToBig(in Fe25519 fe)
    {
        Span<byte> bytes = stackalloc byte[32];
        fe.ToBytes(bytes);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
    }

    private static void WriteLe(BigInteger value, Span<byte> destination)
    {
        destination.Clear();
        var src = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        src.AsSpan(0, System.Math.Min(src.Length, destination.Length)).CopyTo(destination);
    }

    private static BigInteger RandomFieldElement(Random random)
    {
        Span<byte> bytes = stackalloc byte[32];
        random.NextBytes(bytes);
        bytes[31] &= 0x7F; // keep below 2^255 so it reduces predictably
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: false) % P;
    }

    [Fact]
    public void ZeroAndOne_Encode_AsExpected()
    {
        Assert.Equal(BigInteger.Zero, ToBig(Fe25519.Zero));
        Assert.Equal(BigInteger.One, ToBig(Fe25519.One));
    }

    [Fact]
    public void Add_Sub_Mul_Sqr_MatchBigInteger()
    {
        var random = new Random(12345);
        for (var i = 0; i < Iterations; i++)
        {
            var a = RandomFieldElement(random);
            var b = RandomFieldElement(random);
            var fa = ToFe(a);
            var fb = ToFe(b);

            Assert.Equal((a + b) % P, ToBig(Fe25519.Add(fa, fb)));
            Assert.Equal(((a - b) % P + P) % P, ToBig(Fe25519.Sub(fa, fb)));
            Assert.Equal(a * b % P, ToBig(Fe25519.Mul(fa, fb)));
            Assert.Equal(a * a % P, ToBig(Fe25519.Sqr(fa)));
            Assert.Equal((P - a) % P, ToBig(Fe25519.Neg(fa)));
        }
    }

    [Fact]
    public void Invert_MatchesBigInteger_AndIsMultiplicativeInverse()
    {
        var random = new Random(23456);
        for (var i = 0; i < 500; i++)
        {
            var a = RandomFieldElement(random);
            if (a.IsZero)
            {
                continue;
            }

            var fa = ToFe(a);
            var inv = Fe25519.Invert(fa);

            Assert.Equal(BigInteger.ModPow(a, P - 2, P), ToBig(inv));
            Assert.Equal(BigInteger.One, ToBig(Fe25519.Mul(fa, inv)));
        }
    }

    [Fact]
    public void PowPMinus5Div8_MatchesBigInteger()
    {
        var random = new Random(34567);
        var exponent = (P - 5) / 8;
        for (var i = 0; i < 200; i++)
        {
            var a = RandomFieldElement(random);
            var fa = ToFe(a);
            Assert.Equal(BigInteger.ModPow(a, exponent, P), ToBig(Fe25519.PowPMinus5Div8(fa)));
        }
    }

    [Fact]
    public void FromBytes_ToBytes_RoundTrips()
    {
        var random = new Random(45678);
        for (var i = 0; i < Iterations; i++)
        {
            var a = RandomFieldElement(random);
            Assert.Equal(a, ToBig(ToFe(a)));
        }
    }

    [Fact]
    public void IsNegative_MatchesParity()
    {
        var random = new Random(56789);
        for (var i = 0; i < 500; i++)
        {
            var a = RandomFieldElement(random);
            var expected = (ulong)(a % 2);
            Assert.Equal(expected, ToFe(a).IsNegative());
        }
    }

    [Fact]
    public void CMov_And_CSwap_AreConditional()
    {
        var random = new Random(67890);
        var a = ToFe(RandomFieldElement(random));
        var b = ToFe(RandomFieldElement(random));

        var r = a;
        Fe25519.CMov(ref r, b, 0);
        Assert.Equal(ToBig(a), ToBig(r));
        Fe25519.CMov(ref r, b, 1);
        Assert.Equal(ToBig(b), ToBig(r));

        var x = a;
        var y = b;
        Fe25519.CSwap(ref x, ref y, 0);
        Assert.Equal(ToBig(a), ToBig(x));
        Assert.Equal(ToBig(b), ToBig(y));
        Fe25519.CSwap(ref x, ref y, 1);
        Assert.Equal(ToBig(b), ToBig(x));
        Assert.Equal(ToBig(a), ToBig(y));
    }
}
