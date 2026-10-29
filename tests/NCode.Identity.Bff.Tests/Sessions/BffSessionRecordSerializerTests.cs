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

using System.Buffers;
using System.Buffers.Binary;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffSessionRecordSerializerTests : BaseTests
{
    private static BffSessionMetadata CreateMetadata() =>
        new()
        {
            Frontend = "default",
            Issuer = "https://op.example",
            Subject = "sub-1",
            SessionId = "sid-1",
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            LastAccessedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero),
            AbsoluteExpiresAt = new DateTimeOffset(2026, 1, 2, 11, 4, 5, TimeSpan.Zero),
            IdleExpiresAt = new DateTimeOffset(2026, 1, 2, 4, 4, 6, TimeSpan.Zero),
        };

    #region Serialize Tests

    [Fact]
    public void Serialize_Value_StartsWithTheFormatByteAndMetadataLength()
    {
        var metadata = CreateMetadata();
        byte[] payload = [1, 2, 3, 4];
        var expectedMetadata = new ArrayBufferWriter<byte>();
        BffSessionRecordSerializer.WriteMetadata(metadata, expectedMetadata);

        using var lease = BffSessionRecordSerializer.Serialize(metadata, payload, out var value);

        Assert.Equal((byte)1, value.Span[0]);
        Assert.Equal(
            expectedMetadata.WrittenCount,
            BinaryPrimitives.ReadInt32BigEndian(value.Span.Slice(1, 4))
        );
        Assert.Equal(1 + 4 + expectedMetadata.WrittenCount + payload.Length, value.Length);
        Assert.True(
            value
                .Span.Slice(5, expectedMetadata.WrittenCount)
                .SequenceEqual(expectedMetadata.WrittenSpan)
        );
    }

    #endregion

    #region TryDeserialize Tests

    [Fact]
    public void TryDeserialize_RoundTrip_ReturnsTheMetadataAndPayload()
    {
        var metadata = CreateMetadata();
        byte[] payload = [1, 2, 3, 4];

        using var lease = BffSessionRecordSerializer.Serialize(metadata, payload, out var value);

        Assert.True(
            BffSessionRecordSerializer.TryDeserialize(value, out var actual, out var actualPayload)
        );
        Assert.Equal(metadata.Issuer, actual.Issuer);
        Assert.Equal(metadata.Subject, actual.Subject);
        Assert.Equal(metadata.SessionId, actual.SessionId);
        Assert.Equal(metadata.AbsoluteExpiresAt, actual.AbsoluteExpiresAt);
        Assert.Equal(payload, actualPayload.ToArray());
    }

    [Fact]
    public void TryDeserialize_WrongFormatByte_ReturnsFalse()
    {
        using var lease = BffSessionRecordSerializer.Serialize(
            CreateMetadata(),
            [1],
            out var value
        );
        var bytes = value.ToArray();
        bytes[0] = 2;

        Assert.False(BffSessionRecordSerializer.TryDeserialize(bytes, out var metadata, out _));
        Assert.Null(metadata);
    }

    [Fact]
    public void TryDeserialize_TruncatedLength_ReturnsFalse()
    {
        byte[] value = [1, 0, 0];

        Assert.False(BffSessionRecordSerializer.TryDeserialize(value, out _, out _));
    }

    [Fact]
    public void TryDeserialize_LengthPastTheEnd_ReturnsFalse()
    {
        using var lease = BffSessionRecordSerializer.Serialize(
            CreateMetadata(),
            [1],
            out var value
        );
        var bytes = value.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(1, 4), bytes.Length);

        Assert.False(BffSessionRecordSerializer.TryDeserialize(bytes, out _, out _));
    }

    [Fact]
    public void TryDeserialize_InvalidJson_ReturnsFalse()
    {
        byte[] value = [1, 0, 0, 0, 2, (byte)'{', (byte)'x'];

        Assert.False(BffSessionRecordSerializer.TryDeserialize(value, out _, out _));
    }

    #endregion
}
