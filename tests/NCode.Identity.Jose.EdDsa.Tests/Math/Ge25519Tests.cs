// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.EdDsa.Math;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Structural validation of the Ed25519 group: the sqrt(-1) constant, the known base-point encoding, the group order,
// and point decode round-trips. Full cryptographic validation follows from the RFC 8032 sign/verify vectors.
public class Ge25519Tests
{
    // The Ed25519 base point B compressed encoding (little-endian): y = 4/5, x even.
    private static byte[] BaseEncoding()
    {
        var bytes = new byte[32];
        bytes[0] = 0x58;
        for (var i = 1; i < 32; i++)
        {
            bytes[i] = 0x66;
        }

        return bytes;
    }

    // The group order L, little-endian.
    private static byte[] OrderL { get; } =
    [
        0xED,
        0xD3,
        0xF5,
        0x5C,
        0x1A,
        0x63,
        0x12,
        0x58,
        0xD6,
        0x9C,
        0xF7,
        0xA2,
        0xDE,
        0xF9,
        0xDE,
        0x14,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x10,
    ];

    [Fact]
    public void SqrtMinusOne_Squared_IsMinusOne()
    {
        var sq = Fe25519.Sqr(Fe25519.SqrtMinusOne);
        var minusOne = Fe25519.Neg(Fe25519.One);
        Assert.Equal(0UL, Fe25519.Sub(sq, minusOne).IsNonZero());
    }

    [Fact]
    public void BasePoint_Encodes_ToKnownValue()
    {
        Span<byte> encoded = stackalloc byte[32];
        Ge25519.Base.ToBytes(encoded);
        Assert.Equal(BaseEncoding(), encoded.ToArray());
    }

    [Fact]
    public void ScalarMultBase_ByOrder_IsIdentity()
    {
        Span<byte> encoded = stackalloc byte[32];
        Ge25519.ScalarMultBase(OrderL).ToBytes(encoded);

        var expected = new byte[32];
        expected[0] = 0x01; // identity encodes as y = 1, x = 0

        Assert.Equal(expected, encoded.ToArray());
    }

    [Fact]
    public void BasePoint_Decode_RoundTrips()
    {
        Assert.True(Ge25519.TryFromBytes(BaseEncoding(), out var decoded));
        Span<byte> encoded = stackalloc byte[32];
        decoded.ToBytes(encoded);
        Assert.Equal(BaseEncoding(), encoded.ToArray());
    }

    [Fact]
    public void ScalarMultiples_Decode_RoundTrip()
    {
        var random = new Random(97531);
        Span<byte> scalar = stackalloc byte[32];
        Span<byte> encoded = stackalloc byte[32];
        Span<byte> reencoded = stackalloc byte[32];
        for (var i = 0; i < 50; i++)
        {
            random.NextBytes(scalar);
            Ge25519.ScalarMultBase(scalar).ToBytes(encoded);

            Assert.True(Ge25519.TryFromBytes(encoded, out var decoded));
            decoded.ToBytes(reencoded);
            Assert.True(encoded.SequenceEqual(reencoded));
        }
    }
}
