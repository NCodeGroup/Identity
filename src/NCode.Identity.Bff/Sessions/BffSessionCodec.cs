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
using NCode.Buffers;
using NCode.Identity.Bff.Serialization;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Encrypts and decrypts the payload of a session record under a key derived from the session handle, and binds it to
/// the record's store key, version and plaintext metadata.
/// </summary>
internal static class BffSessionCodec
{
    /// <summary>
    /// Derives the store key of a session from its handle.
    /// </summary>
    public static bool TryGetStoreKey(string? handle, [NotNullWhen(true)] out string? storeKey) =>
        BffKeyDerivation.TryDeriveIdentifier(
            handle,
            BffKeyDerivation.SessionIdPurpose,
            out storeKey
        );

    /// <summary>
    /// Builds the record that the store holds for a session.
    /// </summary>
    /// <exception cref="ArgumentException">The handle is not valid.</exception>
    public static BffSessionRecord Encode(
        string handle,
        long version,
        BffSessionMetadata metadata,
        BffSessionPayload payload
    )
    {
        if (!TryGetStoreKey(handle, out var storeKey))
            throw new ArgumentException("The handle is not valid.", nameof(handle));

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonSerializer.Serialize(writer, payload, BffJsonContext.Default.BffSessionPayload);
            writer.Flush();
        }

        var associatedData = CreateAssociatedData(storeKey, version, metadata);
        using var lease = buffer.GetSpanLease(isSensitive: true);
        // The record owns its protected payload beyond this call, so it is an exactly sized array.
        var protectedPayload = new byte[BffPayloadProtector.GetProtectedLength(lease.Span.Length)];
        BffPayloadProtector.Protect(
            handle,
            BffKeyDerivation.SessionPayloadPurpose,
            lease.Span,
            associatedData,
            protectedPayload
        );

        return new BffSessionRecord(storeKey, version, metadata, protectedPayload);
    }

    /// <summary>
    /// Decrypts the payload of a session record, returning <c>false</c> when it does not authenticate.
    /// </summary>
    public static bool TryDecode(
        string handle,
        BffSessionRecord record,
        [NotNullWhen(true)] out BffSessionPayload? payload
    )
    {
        payload = null;

        var associatedData = CreateAssociatedData(record.StoreKey, record.Version, record.Metadata);

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        if (
            !BffPayloadProtector.TryUnprotect(
                handle,
                BffKeyDerivation.SessionPayloadPurpose,
                record.ProtectedPayload.Span,
                associatedData,
                buffer
            )
        )
            return false;

        using var lease = buffer.GetSpanLease(isSensitive: true);
        try
        {
            payload = JsonSerializer.Deserialize(
                lease.Span,
                BffJsonContext.Default.BffSessionPayload
            );
        }
        catch (JsonException)
        {
            return false;
        }

        return payload is not null;
    }

    private static byte[] CreateAssociatedData(
        string storeKey,
        long version,
        BffSessionMetadata metadata
    )
    {
        var keyBytes = SecureEncoding.UTF8.GetBytes(storeKey);
        var metadataBytes = BffSessionRecordSerializer.SerializeMetadata(metadata);

        var result = new byte[keyBytes.Length + sizeof(long) + metadataBytes.Length];
        keyBytes.CopyTo(result, 0);
        BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(keyBytes.Length, sizeof(long)), version);
        metadataBytes.CopyTo(result, keyBytes.Length + sizeof(long));
        return result;
    }
}
