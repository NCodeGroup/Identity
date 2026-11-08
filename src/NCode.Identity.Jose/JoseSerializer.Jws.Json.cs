// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using NCode.Buffers;
using NCode.Disposables;
using NCode.Encoders;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private async ValueTask EncodeJsonJwsAsync(
        IBufferWriter<char> tokenWriter,
        ReadOnlyMemory<byte> payload,
        JoseSigningOptions signingOptions,
        CancellationToken cancellationToken
    )
    {
        /*
           JWS JSON Serialization (RFC 7515 §7.2)

           General:
           {
             "payload": "<BASE64URL(JWS Payload)>",
             "signatures": [
               { "protected": "<b64>", "header": { ... }, "signature": "<b64>" }, ...
             ]
           }

           Flattened:
           {
             "payload": "<BASE64URL(JWS Payload)>",
             "protected": "<b64>",
             "header": { ... },
             "signature": "<b64>"
           }
        */

        var format = signingOptions.Format;
        var signatures = signingOptions.Signatures;

        if (format == JoseSerializationFormat.Flattened && signatures.Count != 1)
            throw new JoseException(
                "The flattened JWS JSON serialization requires exactly one signature."
            );

        var encodePayload = signingOptions.EncodePayload;

        // the payload is shared by every signature, so it is only encoded once
        using var payloadLease = RentJwsPayloadChars(
            encodePayload,
            payload.Span,
            out var encodedPayload
        );

        // every signature is computed first, in order, because a signature may be awaited
        var signed = new List<(string EncodedHeader, string EncodedSignature)>(signatures.Count);
        foreach (var signature in signatures)
        {
            signed.Add(
                await SignJwsSignatureAsync(
                    signature,
                    encodedPayload,
                    encodePayload,
                    cancellationToken
                )
            );
        }

        using var jsonBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false, 1024);

        using (var writer = new Utf8JsonWriter(jsonBuffer))
        {
            writer.WriteStartObject();

            if (!signingOptions.DetachPayload)
                writer.WriteString(JoseClaimNames.Json.Payload, encodedPayload.Span);

            if (format == JoseSerializationFormat.General)
            {
                writer.WritePropertyName(JoseClaimNames.Json.Signatures);
                writer.WriteStartArray();

                foreach (var (signature, parts) in signatures.Zip(signed))
                {
                    writer.WriteStartObject();
                    WriteJwsSignatureMembers(writer, signature, parts);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            else
            {
                WriteJwsSignatureMembers(writer, signatures.First(), signed[0]);
            }

            writer.WriteEndObject();
        }

        EncodeJose(b64: false, jsonBuffer.AsReadOnlySequence, tokenWriter);
    }

    private static IDisposable RentJwsPayloadChars(
        bool encodePayload,
        ReadOnlySpan<byte> payload,
        out ReadOnlyMemory<char> encodedPayload
    )
    {
        var byteCount = payload.Length;
        if (byteCount == 0)
        {
            encodedPayload = ReadOnlyMemory<char>.Empty;
            return Disposable.Empty;
        }

        var charCount = encodePayload
            ? Base64Url.GetCharCountForEncode(byteCount)
            : SecureEncoding.UTF8.GetMaxCharCount(byteCount);

        var lease = BufferFactory.Rent(charCount, isSensitive: true, out Memory<char> buffer);
        try
        {
            int charsWritten;
            if (encodePayload)
            {
                var result = Base64Url.TryEncode(payload, buffer.Span, out charsWritten);
                Debug.Assert(result);
            }
            else
            {
                charsWritten = SecureEncoding.UTF8.GetChars(payload, buffer.Span);
            }

            encodedPayload = buffer[..charsWritten];
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private string EncodeJwsProtectedHeader(JoseSignatureOptions signature, bool encodePayload)
    {
        var (secretKey, signatureAlgorithm) = signature.SigningCredentials;

        var header = BuildJwsProtectedHeader(
            signature,
            signatureAlgorithm.Code,
            secretKey.KeyId,
            encodePayload
        );

        // BASE64URL(UTF8(JWS Protected Header))
        using var headerLease = SerializeToUtf8(header, options: null, out var headerBytes);
        using var encodedHeaderLease = EncodeJose(b64: true, headerBytes, out var encodedHeader);
        return new string(encodedHeader);
    }

    private async ValueTask<(string EncodedHeader, string EncodedSignature)> SignJwsSignatureAsync(
        JoseSignatureOptions signature,
        ReadOnlyMemory<char> encodedPayload,
        bool encodePayload,
        CancellationToken cancellationToken
    )
    {
        var (secretKey, signatureAlgorithm) = signature.SigningCredentials;

        var encodedHeader = EncodeJwsProtectedHeader(signature, encodePayload);

        if (signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits) == 0)
            return (encodedHeader, string.Empty);

        using var inputLease = RentJwsSigningInput(
            encodedHeader,
            encodedPayload.Span,
            out var signingInput
        );

        var encodedSignature = await SignJwsAsync(
            secretKey,
            signatureAlgorithm,
            signingInput,
            cancellationToken
        );

        return (encodedHeader, encodedSignature);
    }

    private void WriteJwsSignatureMembers(
        Utf8JsonWriter writer,
        JoseSignatureOptions signature,
        (string EncodedHeader, string EncodedSignature) signed
    )
    {
        // "protected": BASE64URL(UTF8(JWS Protected Header))
        writer.WriteString(JoseClaimNames.Json.Protected, signed.EncodedHeader);

        // "header": JWS Unprotected Header (optional)
        if (signature.UnprotectedHeaders is not null)
        {
            writer.WritePropertyName(JoseClaimNames.Json.Header);
            WriteJsonHeaderObject(writer, signature.UnprotectedHeaders);
        }

        // "signature": BASE64URL(JWS Signature)
        writer.WriteString(JoseClaimNames.Json.Signature, signed.EncodedSignature);
    }

    private void WriteJsonHeaderObject(
        Utf8JsonWriter writer,
        IEnumerable<KeyValuePair<string, object>> members
    )
    {
        var dictionary =
            members as IReadOnlyDictionary<string, object>
            ?? members.ToDictionary(pair => pair.Key, pair => pair.Value);

        JsonSerializer.Serialize(writer, dictionary, JoseSerializerOptions.JsonSerializerOptions);
    }
}
