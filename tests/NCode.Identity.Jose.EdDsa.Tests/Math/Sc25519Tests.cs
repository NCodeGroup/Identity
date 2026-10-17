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

using System.Globalization;
using System.Numerics;
using NCode.Identity.Jose.EdDsa.Math;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Validates the constant-time scalar arithmetic modulo L against a BigInteger reference over randomized inputs.
public class Sc25519Tests
{
    private static readonly BigInteger L =
        BigInteger.Pow(2, 252)
        + BigInteger.Parse("27742317777372353535851937790883648493", CultureInfo.InvariantCulture);

    private static BigInteger FromLe(ReadOnlySpan<byte> bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: false);

    [Fact]
    public void Reduce_MatchesBigInteger()
    {
        var random = new Random(111);
        Span<byte> wide = stackalloc byte[64];
        Span<byte> result = stackalloc byte[32];
        for (var i = 0; i < 2000; i++)
        {
            random.NextBytes(wide);
            Sc25519.Reduce(wide, result);
            Assert.Equal(FromLe(wide) % L, FromLe(result));
        }
    }

    [Fact]
    public void Reduce_ValueBelowL_IsUnchanged()
    {
        var random = new Random(222);
        Span<byte> wide = stackalloc byte[64];
        Span<byte> result = stackalloc byte[32];
        Span<byte> raw = stackalloc byte[31];
        for (var i = 0; i < 500; i++)
        {
            random.NextBytes(raw);
            var scalar = FromLe(raw) % L; // < 2^248 < L
            wide.Clear();
            WriteLe(scalar, wide);
            Sc25519.Reduce(wide, result);
            Assert.Equal(scalar, FromLe(result));
        }
    }

    [Fact]
    public void MulAdd_MatchesBigInteger()
    {
        var random = new Random(333);
        Span<byte> a = stackalloc byte[32];
        Span<byte> b = stackalloc byte[32];
        Span<byte> c = stackalloc byte[32];
        Span<byte> result = stackalloc byte[32];
        for (var i = 0; i < 2000; i++)
        {
            random.NextBytes(a);
            random.NextBytes(b);
            random.NextBytes(c);
            Sc25519.MulAdd(a, b, c, result);
            var expected = (FromLe(a) * FromLe(b) + FromLe(c)) % L;
            Assert.Equal(expected, FromLe(result));
        }
    }

    private static void WriteLe(BigInteger value, Span<byte> destination)
    {
        destination.Clear();
        var src = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        src.AsSpan(0, System.Math.Min(src.Length, destination.Length)).CopyTo(destination);
    }
}
