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

using NCode.Identity.Jose.EdDsa.Math;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Math;

// Structural validation of the edwards448 group: the base point, the group order, and point decode round-trips.
public class Ge448Tests
{
    // The group order L, 57-byte little-endian.
    private static readonly byte[] OrderL =
    [
        0xF3,
        0x44,
        0x58,
        0xAB,
        0x92,
        0xC2,
        0x78,
        0x23,
        0x55,
        0x8F,
        0xC5,
        0x8D,
        0x72,
        0xC2,
        0x6C,
        0x21,
        0x90,
        0x36,
        0xD6,
        0xAE,
        0x49,
        0xDB,
        0x4E,
        0xC4,
        0xE9,
        0x23,
        0xCA,
        0x7C,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0x3F,
        0x00,
    ];

    private static byte[] IdentityEncoding()
    {
        var bytes = new byte[57];
        bytes[0] = 0x01; // y = 1, x = 0
        return bytes;
    }

    [Fact]
    public void ScalarMultBase_ByOrder_IsIdentity()
    {
        Span<byte> encoded = stackalloc byte[57];
        Ge448.ScalarMultBase(OrderL).ToBytes(encoded);
        Assert.Equal(IdentityEncoding(), encoded.ToArray());
    }

    [Fact]
    public void BasePoint_Decode_RoundTrips()
    {
        Span<byte> encoded = stackalloc byte[57];
        Ge448.Base.ToBytes(encoded);
        Assert.True(Ge448.TryFromBytes(encoded, out var decoded));

        Span<byte> reencoded = stackalloc byte[57];
        decoded.ToBytes(reencoded);
        Assert.True(encoded.SequenceEqual(reencoded));
    }

    [Fact]
    public void ScalarMultiples_Decode_RoundTrip()
    {
        var random = new Random(448555);
        Span<byte> scalar = stackalloc byte[57];
        Span<byte> encoded = stackalloc byte[57];
        Span<byte> reencoded = stackalloc byte[57];
        for (var i = 0; i < 25; i++)
        {
            random.NextBytes(scalar);
            scalar[56] = 0;
            Ge448.ScalarMultBase(scalar).ToBytes(encoded);

            Assert.True(Ge448.TryFromBytes(encoded, out var decoded));
            decoded.ToBytes(reencoded);
            Assert.True(encoded.SequenceEqual(reencoded));
        }
    }
}
