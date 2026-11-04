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
    /// Holds one signature of a <c>COSE_Sign</c> message, or one recipient of a <c>COSE_Mac</c> or <c>COSE_Encrypt</c>
    /// message, as it is written.
    /// </summary>
    public sealed record LayerEntry(
        byte[] ProtectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders,
        byte[] Third
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
        IReadOnlyList<LayerEntry> signatures
    )
    {
        var writer = Begin(tag, 4);
        WriteBody(writer, protectedBytes, unprotectedHeaders, content);
        WriteLayers(writer, signatures);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Writes a <c>COSE_Mac0</c> message.
    /// </summary>
    public static byte[] Mac0(
        ulong? tag,
        byte[] protectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        ReadOnlyMemory<byte>? content,
        byte[] authenticationTag
    ) => Sign1(tag, protectedBytes, unprotectedHeaders, content, authenticationTag);

    /// <summary>
    /// Writes a <c>COSE_Mac</c> message.
    /// </summary>
    public static byte[] Mac(
        ulong? tag,
        byte[] protectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        ReadOnlyMemory<byte>? content,
        byte[] authenticationTag,
        IReadOnlyList<LayerEntry> recipients
    )
    {
        var writer = Begin(tag, 5);
        WriteBody(writer, protectedBytes, unprotectedHeaders, content);
        writer.WriteByteString(authenticationTag);
        WriteLayers(writer, recipients);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Writes a <c>COSE_Encrypt0</c> message.
    /// </summary>
    public static byte[] Encrypt0(
        ulong? tag,
        byte[] protectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        ReadOnlyMemory<byte>? ciphertext
    )
    {
        var writer = Begin(tag, 3);
        WriteBody(writer, protectedBytes, unprotectedHeaders, ciphertext);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Writes a <c>COSE_Encrypt</c> message.
    /// </summary>
    public static byte[] Encrypt(
        ulong? tag,
        byte[] protectedBytes,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        ReadOnlyMemory<byte>? ciphertext,
        IReadOnlyList<LayerEntry> recipients
    )
    {
        var writer = Begin(tag, 4);
        WriteBody(writer, protectedBytes, unprotectedHeaders, ciphertext);
        WriteLayers(writer, recipients);
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

    private static void WriteLayers(CborWriter writer, IReadOnlyList<LayerEntry> layers)
    {
        writer.WriteStartArray(layers.Count);
        foreach (var layer in layers)
        {
            writer.WriteStartArray(3);
            writer.WriteByteString(layer.ProtectedBytes);
            CborMapWriter.WriteMap(writer, layer.UnprotectedHeaders);
            writer.WriteByteString(layer.Third);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
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
