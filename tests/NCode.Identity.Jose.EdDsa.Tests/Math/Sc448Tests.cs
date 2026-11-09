// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Numerics;
using NCode.Identity.Jose.EdDsa.Math;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Validates the constant-time Ed448 scalar arithmetic modulo L against a BigInteger reference.
public class Sc448Tests
{
    private static BigInteger L { get; } =
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
