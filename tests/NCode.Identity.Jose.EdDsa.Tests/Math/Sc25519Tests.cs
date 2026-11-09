// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Numerics;
using NCode.Identity.Jose.EdDsa.Math;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Validates the constant-time scalar arithmetic modulo L against a BigInteger reference over randomized inputs.
public class Sc25519Tests
{
    private static BigInteger L { get; } =
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
