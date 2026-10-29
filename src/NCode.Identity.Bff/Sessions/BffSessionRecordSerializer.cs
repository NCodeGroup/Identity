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
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Bff.Serialization;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Reads and writes the envelope that the session store holds: a format byte, the length of the plaintext metadata,
/// the metadata JSON, and then the protected payload.
/// </summary>
internal static class BffSessionRecordSerializer
{
    private const byte FormatVersion = 1;

    private const int HeaderLength = sizeof(byte) + sizeof(int);

    /// <summary>
    /// Serializes the metadata to the JSON bytes that the payload's associated data covers.
    /// </summary>
    public static byte[] SerializeMetadata(BffSessionMetadata metadata) =>
        JsonSerializer.SerializeToUtf8Bytes(metadata, BffJsonContext.Default.BffSessionMetadata);

    /// <summary>
    /// Writes the metadata as the JSON bytes that the envelope holds in plaintext.
    /// </summary>
    /// <param name="metadata">The metadata to write.</param>
    /// <param name="destination">The buffer that receives the JSON bytes.</param>
    public static void WriteMetadata(BffSessionMetadata metadata, IBufferWriter<byte> destination)
    {
        using var writer = new Utf8JsonWriter(destination);
        JsonSerializer.Serialize(writer, metadata, BffJsonContext.Default.BffSessionMetadata);
    }

    /// <summary>
    /// Writes the stored value for a session into pooled memory.
    /// </summary>
    /// <param name="metadata">The plaintext metadata of the session.</param>
    /// <param name="protectedPayload">The protected payload of the session.</param>
    /// <param name="value">The stored value, which is valid until the returned rental is disposed.</param>
    /// <returns>
    /// The rental that owns <paramref name="value"/>; dispose it after the store has taken the value.
    /// </returns>
    public static IDisposable Serialize(
        BffSessionMetadata metadata,
        ReadOnlySpan<byte> protectedPayload,
        out ReadOnlyMemory<byte> value
    )
    {
        using var metadataBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        WriteMetadata(metadata, metadataBuffer);

        using var metadataLease = metadataBuffer.GetSpanLease(isSensitive: false);
        var metadataSpan = metadataLease.Span;

        var rental = BufferFactory.Rent(
            HeaderLength + metadataSpan.Length + protectedPayload.Length,
            isSensitive: false,
            out Memory<byte> memory
        );

        try
        {
            var span = memory.Span;
            span[0] = FormatVersion;
            BinaryPrimitives.WriteInt32BigEndian(
                span.Slice(sizeof(byte), sizeof(int)),
                metadataSpan.Length
            );
            metadataSpan.CopyTo(span[HeaderLength..]);
            protectedPayload.CopyTo(span[(HeaderLength + metadataSpan.Length)..]);
        }
        catch
        {
            rental.Dispose();
            throw;
        }

        value = memory;
        return rental;
    }

    /// <summary>
    /// Reads a stored value, returning <c>false</c> when it is malformed.
    /// </summary>
    public static bool TryDeserialize(
        ReadOnlyMemory<byte> value,
        [NotNullWhen(true)] out BffSessionMetadata? metadata,
        out ReadOnlyMemory<byte> protectedPayload
    )
    {
        metadata = null;
        protectedPayload = default;

        if (value.Length < HeaderLength || value.Span[0] != FormatVersion)
            return false;

        var metadataLength = BinaryPrimitives.ReadInt32BigEndian(
            value.Span.Slice(sizeof(byte), sizeof(int))
        );
        if (metadataLength < 0 || metadataLength > value.Length - HeaderLength)
            return false;

        BffSessionMetadata? parsed;
        try
        {
            var metadataSlice = value.Span.Slice(HeaderLength, metadataLength);
            parsed = JsonSerializer.Deserialize(
                metadataSlice,
                BffJsonContext.Default.BffSessionMetadata
            );
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null)
            return false;

        metadata = parsed;
        protectedPayload = value[(HeaderLength + metadataLength)..];
        return true;
    }
}
