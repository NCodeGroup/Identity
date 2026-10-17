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

// Validates the constant-time GF(2^448-2^224-1) field arithmetic against a BigInteger reference.
public class Fe448Tests
{
    private static readonly BigInteger P = BigInteger.Pow(2, 448) - BigInteger.Pow(2, 224) - 1;

    private const int Iterations = 1500;

    private static Fe448 ToFe(BigInteger value)
    {
        Span<byte> bytes = stackalloc byte[56];
        WriteLe(value, bytes);
        return Fe448.FromBytes(bytes);
    }

    private static BigInteger ToBig(in Fe448 fe)
    {
        Span<byte> bytes = stackalloc byte[56];
        fe.ToBytes(bytes);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
    }

    private static void WriteLe(BigInteger value, Span<byte> destination)
    {
        destination.Clear();
        var src = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        src.AsSpan(0, System.Math.Min(src.Length, destination.Length)).CopyTo(destination);
    }

    private static BigInteger RandomFieldElement(Random random, Span<byte> scratch)
    {
        random.NextBytes(scratch);
        return new BigInteger(scratch, isUnsigned: true, isBigEndian: false) % P;
    }

    [Fact]
    public void ZeroAndOne_Encode_AsExpected()
    {
        Assert.Equal(BigInteger.Zero, ToBig(Fe448.Zero));
        Assert.Equal(BigInteger.One, ToBig(Fe448.One));
    }

    [Fact]
    public void Add_Sub_Mul_Sqr_Neg_MatchBigInteger()
    {
        var random = new Random(448001);
        Span<byte> scratch = stackalloc byte[56];
        for (var i = 0; i < Iterations; i++)
        {
            var a = RandomFieldElement(random, scratch);
            var b = RandomFieldElement(random, scratch);
            var fa = ToFe(a);
            var fb = ToFe(b);

            Assert.Equal((a + b) % P, ToBig(Fe448.Add(fa, fb)));
            Assert.Equal(((a - b) % P + P) % P, ToBig(Fe448.Sub(fa, fb)));
            Assert.Equal(a * b % P, ToBig(Fe448.Mul(fa, fb)));
            Assert.Equal(a * a % P, ToBig(Fe448.Sqr(fa)));
            Assert.Equal((P - a) % P, ToBig(Fe448.Neg(fa)));
        }
    }

    [Fact]
    public void Invert_MatchesBigInteger()
    {
        var random = new Random(448002);
        Span<byte> scratch = stackalloc byte[56];
        for (var i = 0; i < 300; i++)
        {
            var a = RandomFieldElement(random, scratch);
            if (a.IsZero)
            {
                continue;
            }

            var inv = Fe448.Invert(ToFe(a));
            Assert.Equal(BigInteger.ModPow(a, P - 2, P), ToBig(inv));
            Assert.Equal(BigInteger.One, ToBig(Fe448.Mul(ToFe(a), inv)));
        }
    }

    [Fact]
    public void Sqrt_Exponent_MatchesBigInteger()
    {
        var random = new Random(448003);
        var exponent = (P + 1) / 4;
        Span<byte> scratch = stackalloc byte[56];
        for (var i = 0; i < 200; i++)
        {
            var a = RandomFieldElement(random, scratch);
            Assert.Equal(BigInteger.ModPow(a, exponent, P), ToBig(Fe448.Sqrt(ToFe(a))));
        }
    }

    [Fact]
    public void FromBytes_ToBytes_RoundTrips()
    {
        var random = new Random(448004);
        Span<byte> scratch = stackalloc byte[56];
        for (var i = 0; i < Iterations; i++)
        {
            var a = RandomFieldElement(random, scratch);
            Assert.Equal(a, ToBig(ToFe(a)));
        }
    }
}
