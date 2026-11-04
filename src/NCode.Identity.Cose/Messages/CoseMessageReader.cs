// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Formats.Cbor;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Options;

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Parses the six COSE message structures (RFC 9052 §2) strictly and within the configured limits. Nothing is verified
/// here; the parser enforces the shape and the header rules that need no key or algorithm.
/// </summary>
// See ADR-1409.
internal static class CoseMessageReader
{
    /// <summary>
    /// Parses a message.
    /// </summary>
    /// <param name="message">The encoded message.</param>
    /// <param name="untaggedMessageType">The type to assume when the message carries no tag.</param>
    /// <param name="options">The limits.</param>
    /// <returns>The parsed message.</returns>
    /// <exception cref="CoseException">The message is malformed or exceeds a limit.</exception>
    public static ParsedCoseMessage Read(
        ReadOnlyMemory<byte> message,
        CoseMessageType? untaggedMessageType,
        CoseOptions options
    )
    {
        CborGuard.CheckSize(message, options);

        try
        {
            var reader = CborGuard.NewReader(message);
            var (messageType, isTagged) = ReadType(reader, untaggedMessageType);
            var parsed = ReadMessage(reader, messageType, isTagged, options);
            if (reader.BytesRemaining != 0)
                throw CborGuard.Invalid("The input has bytes after the message.");

            return parsed;
        }
        catch (Exception exception) when (CborGuard.IsReaderException(exception))
        {
            throw CborGuard.Invalid("The COSE message is malformed.", exception);
        }
    }

    private static (CoseMessageType MessageType, bool IsTagged) ReadType(
        CborReader reader,
        CoseMessageType? untaggedMessageType
    )
    {
        if (reader.PeekState() != CborReaderState.Tag)
        {
            return untaggedMessageType is { } assumed
                ? (assumed, false)
                : throw new CoseException(
                    CoseFailureCodes.UntaggedMessage,
                    "The message carries no COSE tag and its type was not named."
                );
        }

        var tag = reader.ReadTag();
        CoseMessageType? type = (ulong)tag switch
        {
            CoseTags.Sign => CoseMessageType.Sign,
            CoseTags.Sign1 => CoseMessageType.Sign1,
            CoseTags.Encrypt => CoseMessageType.Encrypt,
            CoseTags.Encrypt0 => CoseMessageType.Encrypt0,
            CoseTags.Mac => CoseMessageType.Mac,
            CoseTags.Mac0 => CoseMessageType.Mac0,
            _ => null,
        };

        return type is { } known
            ? (known, true)
            : throw new CoseException(
                CoseFailureCodes.UnsupportedMessageType,
                "The message carries a tag that is not a COSE message tag."
            );
    }

    private static ParsedCoseMessage ReadMessage(
        CborReader reader,
        CoseMessageType messageType,
        bool isTagged,
        CoseOptions options
    )
    {
        var expectedLength = messageType switch
        {
            CoseMessageType.Sign1
            or CoseMessageType.Mac0
            or CoseMessageType.Sign
            or CoseMessageType.Encrypt => 4,
            CoseMessageType.Mac => 5,
            _ => 3,
        };

        if (reader.PeekState() != CborReaderState.StartArray)
            throw CborGuard.Invalid("The COSE message is not an array.");

        var length = reader.ReadStartArray();
        if (length != expectedLength)
            throw CborGuard.Invalid("The COSE message array has the wrong number of elements.");

        var body = ReadLayer(
            reader,
            messageType == CoseMessageType.Sign ? CoseLayerKind.SignBody : CoseLayerKind.Body,
            options
        );
        var content = ReadContent(reader);

        ReadOnlyMemory<byte> authenticator = default;
        IReadOnlyList<ParsedCoseLayer> signatures = [];
        IReadOnlyList<ParsedCoseLayer> recipients = [];
        switch (messageType)
        {
            case CoseMessageType.Sign1:
            case CoseMessageType.Mac0:
                authenticator = ReadByteString(reader);
                break;
            case CoseMessageType.Sign:
                signatures = ReadLayers(
                    reader,
                    CoseLayerKind.Signature,
                    options.MaxSignatures,
                    options
                );
                break;
            case CoseMessageType.Mac:
                authenticator = ReadByteString(reader);
                recipients = ReadLayers(
                    reader,
                    CoseLayerKind.Recipient,
                    options.MaxRecipients,
                    options
                );
                break;
            case CoseMessageType.Encrypt:
                recipients = ReadLayers(
                    reader,
                    CoseLayerKind.Recipient,
                    options.MaxRecipients,
                    options
                );
                break;
        }

        reader.ReadEndArray();
        return new ParsedCoseMessage
        {
            MessageType = messageType,
            IsTagged = isTagged,
            Body = body,
            Content = content,
            Authenticator = authenticator,
            Signatures = signatures,
            Recipients = recipients,
        };
    }

    private static ReadOnlyMemory<byte>? ReadContent(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return ReadByteString(reader);
    }

    private static ReadOnlyMemory<byte> ReadByteString(CborReader reader)
    {
        if (reader.PeekState() != CborReaderState.ByteString)
            throw CborGuard.Invalid("A COSE message field is not a byte string.");

        return reader.ReadByteString();
    }

    private static List<ParsedCoseLayer> ReadLayers(
        CborReader reader,
        CoseLayerKind kind,
        int maximum,
        CoseOptions options
    )
    {
        if (reader.PeekState() != CborReaderState.StartArray)
            throw CborGuard.Invalid("The signatures or recipients are not an array.");

        var length =
            reader.ReadStartArray()
            ?? throw CborGuard.Invalid("The signatures or recipients use an indefinite length.");
        if (length < 1)
            throw CborGuard.Invalid("The signatures or recipients array is empty.");

        if (length > maximum)
            throw new CoseException(
                CoseFailureCodes.LimitExceeded,
                kind == CoseLayerKind.Signature
                    ? "The message carries more signatures than the configured maximum."
                    : "The message carries more recipients than the configured maximum."
            );

        var layers = new List<ParsedCoseLayer>(length);
        for (var index = 0; index < length; index++)
        {
            if (reader.PeekState() != CborReaderState.StartArray)
                throw CborGuard.Invalid("A signature or recipient is not an array.");

            var elementLength = reader.ReadStartArray();
            if (kind == CoseLayerKind.Recipient && elementLength == 4)
                throw new CoseException(
                    CoseFailureCodes.InvalidHeader,
                    "A recipient carries recipients of its own, which this library does not support."
                );

            if (elementLength != 3)
                throw CborGuard.Invalid(
                    "A signature or recipient has the wrong number of elements."
                );

            var headers = ReadLayer(reader, kind, options);
            layers.Add(
                new ParsedCoseLayer
                {
                    ProtectedBytes = headers.ProtectedBytes,
                    Protected = headers.Protected,
                    Unprotected = headers.Unprotected,
                    Third = ReadByteString(reader),
                }
            );
            reader.ReadEndArray();
        }

        reader.ReadEndArray();
        return layers;
    }

    private static ParsedCoseLayer ReadLayer(
        CborReader reader,
        CoseLayerKind kind,
        CoseOptions options
    )
    {
        var protectedBytes = ReadByteString(reader);
        var protectedHeaders =
            protectedBytes.Length == 0
                ? FrozenDictionary<CoseLabel, CoseValue>.Empty
                : CborGuard.ReadRootMap(protectedBytes, options);

        if (reader.PeekState() != CborReaderState.StartMap)
            throw CborGuard.Invalid("The unprotected header bucket is not a map.");

        var unprotectedHeaders = CborGuard.ReadMap(reader, options.MaxNestingDepth);

        CoseHeaderPolicy.ValidateRead(protectedHeaders, unprotectedHeaders);

        if (
            kind == CoseLayerKind.SignBody
            && (
                protectedHeaders.ContainsKey(CoseHeaderLabels.Algorithm)
                || unprotectedHeaders.ContainsKey(CoseHeaderLabels.Algorithm)
            )
        )
            throw new CoseException(
                CoseFailureCodes.InvalidHeader,
                "The body of a COSE_Sign message carries an algorithm; the algorithm belongs to each signature."
            );

        if (kind == CoseLayerKind.Recipient && protectedHeaders.Count != 0)
            throw new CoseException(
                CoseFailureCodes.InvalidHeader,
                "The protected header bucket of a recipient is not empty."
            );

        return new ParsedCoseLayer
        {
            ProtectedBytes = protectedBytes,
            Protected = protectedHeaders,
            Unprotected = unprotectedHeaders,
        };
    }
}
