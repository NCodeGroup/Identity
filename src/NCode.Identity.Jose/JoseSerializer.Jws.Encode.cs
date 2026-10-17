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
using System.Text;
using System.Text.Json;
using NCode.Buffers;
using NCode.Disposables;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Encoders;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Json;
using Nerdbank.Streams;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    /// <inheritdoc />
    public JoseEncoder CreateEncoder(JoseSigningOptions signingOptions) =>
        new JoseSigningEncoder(this, signingOptions);

    internal void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseSigningOptions signingOptions
    )
    {
        if (signingOptions.Signatures.Count == 0)
            throw new JoseException("At least one signature is required to encode a JWS token.");

        switch (signingOptions.Format)
        {
            case JoseSerializationFormat.Compact:
                EncodeCompactJws(tokenWriter, payload, signingOptions);
                break;

            case JoseSerializationFormat.Flattened:
            case JoseSerializationFormat.General:
                EncodeJsonJws(tokenWriter, payload, signingOptions);
                break;

            default:
                throw new JoseException(
                    $"The '{signingOptions.Format}' serialization format is not supported for JWS tokens."
                );
        }
    }

    private void EncodeCompactJws(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseSigningOptions signingOptions
    )
    {
        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */

        if (signingOptions.Signatures.Count != 1)
            throw new JoseException(
                "The compact JWS serialization requires exactly one signature."
            );

        var signature = signingOptions.Signatures.First();
        if (signature.UnprotectedHeaders is not null)
            throw new JoseException(
                "The compact JWS serialization cannot carry an unprotected header."
            );

        var signatureCredentials = signature.SigningCredentials;
        var (secretKey, signatureAlgorithm) = signatureCredentials;

        var header = BuildJwsProtectedHeader(
            signature,
            signatureAlgorithm.Code,
            secretKey.KeyId,
            signingOptions.EncodePayload
        );

        // BASE64URL(UTF8(JWS Protected Header)) || '.'
        using var headerLease = SerializeToUtf8(header, options: null, out var headerBytes);
        var encodedHeaderPart = WriteCompactSegment(headerBytes, tokenWriter);

        // BASE64URL(JWS Payload) || '.'
        using var _ = EncodeJwsPayload(
            payload,
            tokenWriter,
            signingOptions,
            out var encodedPayloadPart
        );

        // BASE64URL(JWS Signature)
        EncodeJwsSignature(
            secretKey,
            signatureAlgorithm,
            encodedHeaderPart,
            encodedPayloadPart,
            tokenWriter
        );
    }

    private static Dictionary<string, object> BuildJwsProtectedHeader(
        JoseSignatureOptions signature,
        string algorithmCode,
        string? keyId,
        bool encodePayload
    )
    {
        var protectedHeaders = signature.ProtectedHeaders;
        var header =
            protectedHeaders != null
                ? new Dictionary<string, object>(protectedHeaders)
                : new Dictionary<string, object>();

        var tokenType = signature.TokenType;
        if (!string.IsNullOrEmpty(tokenType))
            header[JoseClaimNames.Header.Typ] = tokenType;

        if (!string.IsNullOrEmpty(algorithmCode))
            header[JoseClaimNames.Header.Alg] = algorithmCode;

        if (!string.IsNullOrEmpty(keyId) && signature.AddKeyIdHeader)
            header[JoseClaimNames.Header.Kid] = keyId;

        if (!encodePayload)
        {
            var critical = new HashSet<string> { JoseClaimNames.Header.B64 };
            if (
                header.TryGetValue<IEnumerable<string>>(
                    JoseClaimNames.Header.Crit,
                    out var existing
                )
            )
            {
                critical.UnionWith(existing);
            }

            header[JoseClaimNames.Header.B64] = false;
            header[JoseClaimNames.Header.Crit] = critical;
        }

        return header;
    }

    private static IDisposable EncodeJwsPayload(
        ReadOnlySpan<byte> payload,
        IBufferWriter<char> tokenWriter,
        JoseSigningOptions signingOptions,
        out ReadOnlySpan<char> encodedPayloadPart
    )
    {
        var payloadCharCount = signingOptions.EncodePayload
            ? Base64Url.GetCharCountForEncode(payload.Length)
            : SecureEncoding.UTF8.GetCharCount(payload);

        IDisposable owner;
        Span<char> workingPayload;
        int payloadCharsWritten;

        if (signingOptions.DetachPayload)
        {
            var lease = MemoryPool<char>.Shared.Rent(payloadCharCount);
            workingPayload = lease.Memory.Span;
            owner = lease;
        }
        else
        {
            workingPayload = tokenWriter.GetSpan(payloadCharCount + 1); // with dot
            owner = Disposable.Empty;
        }

        if (signingOptions.EncodePayload)
        {
            var result = Base64Url.TryEncode(payload, workingPayload, out payloadCharsWritten);
            Debug.Assert(result && payloadCharsWritten == payloadCharCount);
        }
        else
        {
            payloadCharsWritten = SecureEncoding.UTF8.GetChars(payload, workingPayload);
            Debug.Assert(payloadCharsWritten == payloadCharCount);
        }

        if (signingOptions.DetachPayload)
        {
            var span = tokenWriter.GetSpan(1);
            span[0] = '.';
            tokenWriter.Advance(1);
        }
        else
        {
            workingPayload[payloadCharsWritten] = '.';
            tokenWriter.Advance(payloadCharsWritten + 1); // with dot
        }

        encodedPayloadPart = workingPayload[..payloadCharsWritten]; // without dot
        return owner;
    }

    private static void EncodeJwsSignature(
        SecretKey secretKey,
        SignatureAlgorithm signatureAlgorithm,
        ReadOnlySpan<char> encodedHeaderPart,
        ReadOnlySpan<char> encodedPayloadPart,
        IBufferWriter<char> tokenWriter
    )
    {
        var signatureByteCount = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        var isEmptySignature = signatureByteCount == 0;
        if (isEmptySignature)
            return;

        // Input Data for Signature
        // b64 true:  ASCII( BASE64URL(UTF8(JWS Protected Header)) || '.' || BASE64URL(JWS Payload) )
        // b64 false: ASCII( BASE64URL(UTF8(JWS Protected Header)) || '.' ) || JWS Payload

        var headerByteCount = Encoding.ASCII.GetByteCount(encodedHeaderPart);
        var payloadByteCount = Encoding.ASCII.GetByteCount(encodedPayloadPart);

        var inputByteCount = headerByteCount + payloadByteCount;
        using var inputLease = BufferFactory.Rent(
            inputByteCount,
            isSensitive: false,
            out Span<byte> inputData
        );

        var headerWritten = Encoding.ASCII.GetBytes(encodedHeaderPart, inputData);
        Debug.Assert(headerWritten == headerByteCount);

        var payloadWritten = Encoding.ASCII.GetBytes(
            encodedPayloadPart,
            inputData[headerByteCount..]
        );
        Debug.Assert(payloadWritten == payloadByteCount);

        using var signatureLease = BufferFactory.Rent(
            signatureByteCount,
            isSensitive: false,
            out Span<byte> signatureBytes
        );
        var signResult = signatureAlgorithm.TrySign(
            secretKey,
            inputData,
            signatureBytes,
            out var signatureBytesWritten
        );
        Debug.Assert(signResult && signatureBytesWritten == signatureByteCount);

        var signatureCharCount = Base64Url.GetCharCountForEncode(signatureByteCount);
        var encodedSignature = tokenWriter.GetSpan(signatureCharCount);

        var encodeResult = Base64Url.TryEncode(
            signatureBytes,
            encodedSignature,
            out var signatureCharsWritten
        );
        Debug.Assert(encodeResult && signatureCharsWritten == signatureCharCount);

        tokenWriter.Advance(signatureCharsWritten);
    }
}
