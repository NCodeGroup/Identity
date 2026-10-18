#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using System.Diagnostics;
using System.Text.Json;
using NCode.Buffers;
using NCode.Disposables;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private void EncodeJsonJws(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseSigningOptions signingOptions
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
        using var payloadLease = EncodeJose(encodePayload, payload, out var encodedPayload);

        using var jsonBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false, 1024);

        using (var writer = new Utf8JsonWriter(jsonBuffer))
        {
            writer.WriteStartObject();

            if (!signingOptions.DetachPayload)
                writer.WriteString(JoseClaimNames.Json.Payload, encodedPayload);

            if (format == JoseSerializationFormat.General)
            {
                writer.WritePropertyName(JoseClaimNames.Json.Signatures);
                writer.WriteStartArray();

                foreach (var signature in signatures)
                {
                    writer.WriteStartObject();
                    WriteJwsSignatureMembers(writer, signature, encodedPayload, encodePayload);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            else
            {
                WriteJwsSignatureMembers(writer, signatures.First(), encodedPayload, encodePayload);
            }

            writer.WriteEndObject();
        }

        EncodeJose(b64: false, jsonBuffer.AsReadOnlySequence, tokenWriter);
    }

    private void WriteJwsSignatureMembers(
        Utf8JsonWriter writer,
        JoseSignatureOptions signature,
        ReadOnlySpan<char> encodedPayload,
        bool encodePayload
    )
    {
        var (secretKey, signatureAlgorithm) = signature.SigningCredentials;

        var header = BuildJwsProtectedHeader(
            signature,
            signatureAlgorithm.Code,
            secretKey.KeyId,
            encodePayload
        );

        // "protected": BASE64URL(UTF8(JWS Protected Header))
        using var headerLease = SerializeToUtf8(header, options: null, out var headerBytes);
        using var encodedHeaderLease = EncodeJose(b64: true, headerBytes, out var encodedHeader);
        writer.WriteString(JoseClaimNames.Json.Protected, encodedHeader);

        // "header": JWS Unprotected Header (optional)
        if (signature.UnprotectedHeaders is not null)
        {
            writer.WritePropertyName(JoseClaimNames.Json.Header);
            WriteJsonHeaderObject(writer, signature.UnprotectedHeaders);
        }

        // "signature": BASE64URL(JWS Signature)
        using var signatureLease = ComputeJwsSignature(
            secretKey,
            signatureAlgorithm,
            encodedHeader,
            encodedPayload,
            out var encodedSignature
        );
        writer.WriteString(JoseClaimNames.Json.Signature, encodedSignature);
    }

    private static IDisposable ComputeJwsSignature(
        SecretKey secretKey,
        SignatureAlgorithm signatureAlgorithm,
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        out ReadOnlySpan<char> encodedSignature
    )
    {
        var signatureByteCount = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        if (signatureByteCount == 0)
        {
            encodedSignature = ReadOnlySpan<char>.Empty;
            return Disposable.Empty;
        }

        using var inputLease = GetSignatureInput(
            encodedHeader,
            encodedPayload,
            out var signatureInput
        );

        using var signatureBytesLease = BufferFactory.Rent(
            signatureByteCount,
            isSensitive: false,
            out Span<byte> signatureBytes
        );

        var signResult = signatureAlgorithm.TrySign(
            secretKey,
            signatureInput,
            signatureBytes,
            out var signatureBytesWritten
        );
        Debug.Assert(signResult && signatureBytesWritten == signatureByteCount);

        return EncodeJose(b64: true, signatureBytes, out encodedSignature);
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
