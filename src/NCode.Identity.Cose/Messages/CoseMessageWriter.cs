// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Cbor;
using NCode.Identity.Cose.Cbor;

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Writes the COSE message structures (RFC 9052 §2) in the deterministic encoding.
/// </summary>
// See ADR-1409.
internal static class CoseMessageWriter
{
    /// <summary>
    /// Holds one signature of a <c>COSE_Sign</c> message as it is written.
    /// </summary>
    public sealed record SignatureEntry(
        byte[] ProtectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders,
        byte[] Signature
    );

    /// <summary>
    /// Writes a <c>COSE_Sign1</c> message.
    /// </summary>
    public static byte[] Sign1(
        ulong? tag,
        byte[] protectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        ReadOnlyMemory<byte>? content,
        byte[] signature
    )
    {
        var writer = Begin(tag, 4);
        WriteBody(writer, protectedBytes, unprotectedHeaders, content);
        writer.WriteByteString(signature);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Writes a <c>COSE_Sign</c> message.
    /// </summary>
    public static byte[] Sign(
        ulong? tag,
        byte[] protectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        ReadOnlyMemory<byte>? content,
        IReadOnlyList<SignatureEntry> signatures
    )
    {
        var writer = Begin(tag, 4);
        WriteBody(writer, protectedBytes, unprotectedHeaders, content);
        writer.WriteStartArray(signatures.Count);
        foreach (var signature in signatures)
        {
            writer.WriteStartArray(3);
            writer.WriteByteString(signature.ProtectedBytes);
            CborMapWriter.WriteMap(writer, signature.UnprotectedHeaders);
            writer.WriteByteString(signature.Signature);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndArray();
        return writer.Encode();
    }

    private static CborWriter Begin(ulong? tag, int arrayLength)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        if (tag is { } value)
            writer.WriteTag((CborTag)value);

        writer.WriteStartArray(arrayLength);
        return writer;
    }

    private static void WriteBody(
        CborWriter writer,
        byte[] protectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        ReadOnlyMemory<byte>? content
    )
    {
        writer.WriteByteString(protectedBytes);
        CborMapWriter.WriteMap(writer, unprotectedHeaders);
        if (content is { } bytes)
            writer.WriteByteString(bytes.Span);
        else
            writer.WriteNull();
    }
}
