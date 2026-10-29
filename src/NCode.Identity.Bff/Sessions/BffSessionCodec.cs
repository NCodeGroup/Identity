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
using System.Text;
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

        using var associatedData = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        WriteAssociatedData(storeKey, version, metadata, associatedData);

        using var lease = buffer.GetSpanLease(isSensitive: true);
        using var associatedDataLease = associatedData.GetSpanLease(isSensitive: false);
        // The record owns its protected payload beyond this call, so it is an exactly sized array.
        var protectedPayload = new byte[BffPayloadProtector.GetProtectedLength(lease.Span.Length)];
        BffPayloadProtector.Protect(
            handle,
            BffKeyDerivation.SessionPayloadPurpose,
            lease.Span,
            associatedDataLease.Span,
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

        using var associatedData = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        WriteAssociatedData(record.StoreKey, record.Version, record.Metadata, associatedData);

        using var associatedDataLease = associatedData.GetSpanLease(isSensitive: false);
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        if (
            !BffPayloadProtector.TryUnprotect(
                handle,
                BffKeyDerivation.SessionPayloadPurpose,
                record.ProtectedPayload.Span,
                associatedDataLease.Span,
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

    /// <summary>
    /// Writes the associated data, which binds the payload to the store key, the version and the plaintext metadata.
    /// </summary>
    /// <param name="storeKey">The store key of the session.</param>
    /// <param name="version">The version of the record.</param>
    /// <param name="metadata">The plaintext metadata of the record.</param>
    /// <param name="destination">The buffer that receives the associated data.</param>
    private static void WriteAssociatedData<TWriter>(
        string storeKey,
        long version,
        BffSessionMetadata metadata,
        TWriter destination
    )
        where TWriter : IBufferWriter<byte>
    {
        _ = SecureEncoding.UTF8.GetBytes(storeKey, destination);

        BinaryPrimitives.WriteInt64BigEndian(destination.GetSpan(sizeof(long)), version);
        destination.Advance(sizeof(long));

        BffSessionRecordSerializer.WriteMetadata(metadata, destination);
    }
}
