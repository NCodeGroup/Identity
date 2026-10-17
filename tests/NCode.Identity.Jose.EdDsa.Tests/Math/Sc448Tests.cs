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

// Validates the constant-time Ed448 scalar arithmetic modulo L against a BigInteger reference.
public class Sc448Tests
{
    private static readonly BigInteger L =
        BigInteger.Pow(2, 446)
        - BigInteger.Parse(
            "13818066809895115352007386748515426880336692474882178609894547503885",
            CultureInfo.InvariantCulture
        );

    private static BigInteger FromLe(ReadOnlySpan<byte> bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: false);

    [Fact]
    public void Reduce_MatchesBigInteger()
    {
        var random = new Random(4480);
        Span<byte> wide = stackalloc byte[114];
        Span<byte> result = stackalloc byte[57];
        for (var i = 0; i < 1000; i++)
        {
            random.NextBytes(wide);
            Sc448.Reduce(wide, result);
            Assert.Equal(FromLe(wide) % L, FromLe(result));
        }
    }

    [Fact]
    public void MulAdd_MatchesBigInteger()
    {
        var random = new Random(4481);
        Span<byte> a = stackalloc byte[57];
        Span<byte> b = stackalloc byte[57];
        Span<byte> c = stackalloc byte[57];
        Span<byte> result = stackalloc byte[57];
        for (var i = 0; i < 1000; i++)
        {
            random.NextBytes(a);
            random.NextBytes(b);
            random.NextBytes(c);
            Sc448.MulAdd(a, b, c, result);
            var expected = (FromLe(a) * FromLe(b) + FromLe(c)) % L;
            Assert.Equal(expected, FromLe(result));
        }
    }

    [Fact]
    public void IsCanonical_DistinguishesBelowAndAtOrAboveL()
    {
        Span<byte> below = stackalloc byte[57];
        WriteLe(L - 1, below);
        Assert.True(Sc448.IsCanonical(below));

        Span<byte> atL = stackalloc byte[57];
        WriteLe(L, atL);
        Assert.False(Sc448.IsCanonical(atL));
    }

    private static void WriteLe(BigInteger value, Span<byte> destination)
    {
        destination.Clear();
        var src = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        src.AsSpan(0, System.Math.Min(src.Length, destination.Length)).CopyTo(destination);
    }
}
