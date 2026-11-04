// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Cbor;

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Builds the structures that a COSE signature, authentication code or encryption covers (RFC 9052 §4.4, §5.3, §6.3).
/// Each is written in the deterministic encoding, with an empty protected bucket as a zero-length byte string.
/// </summary>
// See ADR-1409.
internal static class CoseStructures
{
    /// <summary>
    /// Builds the <c>Sig_structure</c> of a <c>COSE_Sign1</c> message.
    /// </summary>
    public static byte[] Sign1(
        ReadOnlySpan<byte> bodyProtected,
        ReadOnlySpan<byte> externalAad,
        ReadOnlySpan<byte> payload
    )
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(4);
        writer.WriteTextString("Signature1");
        writer.WriteByteString(bodyProtected);
        writer.WriteByteString(externalAad);
        writer.WriteByteString(payload);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Builds the <c>Sig_structure</c> of one signature of a <c>COSE_Sign</c> message.
    /// </summary>
    public static byte[] Signature(
        ReadOnlySpan<byte> bodyProtected,
        ReadOnlySpan<byte> signProtected,
        ReadOnlySpan<byte> externalAad,
        ReadOnlySpan<byte> payload
    )
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(5);
        writer.WriteTextString("Signature");
        writer.WriteByteString(bodyProtected);
        writer.WriteByteString(signProtected);
        writer.WriteByteString(externalAad);
        writer.WriteByteString(payload);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Builds the <c>MAC_structure</c> of a <c>COSE_Mac0</c> or <c>COSE_Mac</c> message.
    /// </summary>
    public static byte[] Mac(
        string context,
        ReadOnlySpan<byte> bodyProtected,
        ReadOnlySpan<byte> externalAad,
        ReadOnlySpan<byte> payload
    )
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(4);
        writer.WriteTextString(context);
        writer.WriteByteString(bodyProtected);
        writer.WriteByteString(externalAad);
        writer.WriteByteString(payload);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Builds the <c>Enc_structure</c> of a <c>COSE_Encrypt0</c> or <c>COSE_Encrypt</c> message, which is the additional
    /// authenticated data of its content encryption.
    /// </summary>
    public static byte[] Encrypt(
        string context,
        ReadOnlySpan<byte> bodyProtected,
        ReadOnlySpan<byte> externalAad
    )
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(3);
        writer.WriteTextString(context);
        writer.WriteByteString(bodyProtected);
        writer.WriteByteString(externalAad);
        writer.WriteEndArray();
        return writer.Encode();
    }
}
