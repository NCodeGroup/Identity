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

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
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
    /// Writes the stored value for a session.
    /// </summary>
    public static byte[] Serialize(BffSessionMetadata metadata, ReadOnlySpan<byte> protectedPayload)
    {
        var metadataBytes = SerializeMetadata(metadata);

        var result = new byte[HeaderLength + metadataBytes.Length + protectedPayload.Length];
        result[0] = FormatVersion;
        BinaryPrimitives.WriteInt32BigEndian(
            result.AsSpan(sizeof(byte), sizeof(int)),
            metadataBytes.Length
        );
        metadataBytes.CopyTo(result.AsSpan(HeaderLength));
        protectedPayload.CopyTo(result.AsSpan(HeaderLength + metadataBytes.Length));
        return result;
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
