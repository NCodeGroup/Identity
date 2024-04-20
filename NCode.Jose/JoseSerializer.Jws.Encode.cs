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
using NCode.Encoders;
using NCode.Jose.Algorithms;
using NCode.Jose.Buffers;
using NCode.Jose.Encoders;
using NCode.Jose.Extensions;
using NCode.Jose.Infrastructure;
using NCode.Jose.SecretKeys;
using Nerdbank.Streams;

namespace NCode.Jose;

partial class JoseSerializer
{
    /// <inheritdoc />
    public JoseEncoder CreateEncoder(JoseSigningOptions signingOptions) =>
        new JoseSigningEncoder(this, signingOptions);

    /// <inheritdoc />
    public string Encode<T>(
        T payload,
        JoseSigningOptions signingOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        using var _ = SerializeToUtf8(
            payload,
            jsonOptions,
            out var payloadBytes);
        Encode(
            tokenBuffer,
            payloadBytes,
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void Encode<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        JoseSigningOptions signingOptions,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var _ = SerializeToUtf8(
            payload,
            jsonOptions,
            out var bytes);
        Encode(
            tokenWriter,
            bytes,
            signingOptions,
            extraHeaders);
    }

    /// <inheritdoc />
    public string Encode(
        string payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        Encode(
            tokenBuffer,
            payload.AsSpan(),
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void Encode(
        IBufferWriter<char> tokenWriter,
        string payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        Encode(
            tokenWriter,
            payload.AsSpan(),
            signingOptions,
            extraHeaders);
    }

    /// <inheritdoc />
    public string Encode(
        ReadOnlySpan<char> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        Encode(
            tokenBuffer,
            payload,
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var byteCount = SecureEncoding.Utf8.GetByteCount(payload);
        using var payloadLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = SecureEncoding.Utf8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        Encode(
            tokenWriter,
            payloadBytes,
            signingOptions,
            extraHeaders);
    }

    /// <inheritdoc />
    public string Encode(
        ReadOnlySpan<byte> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        Encode(
            tokenBuffer,
            payload,
            signingOptions,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */

        var signatureCredentials = signingOptions.SigningCredentials;
        var (secretKey, signatureAlgorithm) = signatureCredentials;

        // BASE64URL(UTF8(JWS Protected Header)) || '.'
        EncodeJwsHeader(
            signatureAlgorithm.Code,
            secretKey.KeyId,
            tokenWriter,
            signingOptions,
            extraHeaders,
            out var encodedHeaderPart);

        // BASE64URL(JWS Payload) || '.'
        using var _ = EncodeJwsPayload(
            payload,
            tokenWriter,
            signingOptions,
            out var encodedPayloadPart);

        // BASE64URL(JWS Signature)
        EncodeJwsSignature(
            secretKey,
            signatureAlgorithm,
            encodedHeaderPart,
            encodedPayloadPart,
            tokenWriter);
    }

    private void EncodeJwsHeader(
        string algorithmCode,
        string? keyId,
        IBufferWriter<char> tokenWriter,
        JoseSigningOptions signingOptions,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders,
        out ReadOnlySpan<char> encodedHeaderPart)
    {
        var header = extraHeaders != null ?
            new Dictionary<string, object>(extraHeaders) :
            new Dictionary<string, object>();

        var tokenType = signingOptions.TokenType;
        if (!string.IsNullOrEmpty(tokenType))
            header[JoseClaimNames.Header.Typ] = tokenType;

        if (!string.IsNullOrEmpty(algorithmCode))
            header[JoseClaimNames.Header.Alg] = algorithmCode;

        if (!string.IsNullOrEmpty(keyId) && signingOptions.AddKeyIdHeader)
            header[JoseClaimNames.Header.Kid] = keyId;

        if (!signingOptions.EncodePayload)
        {
            var critical = new HashSet<string> { JoseClaimNames.Header.B64 };
            if (header.TryGetValue<IEnumerable<string>>(JoseClaimNames.Header.Crit, out var existing))
            {
                critical.UnionWith(existing);
            }

            header[JoseClaimNames.Header.B64] = false;
            header[JoseClaimNames.Header.Crit] = critical;
        }

        using var headerLease = SerializeToUtf8(header, options: null, out var headerBytes);
        encodedHeaderPart = WriteCompactSegment(headerBytes, tokenWriter);
    }

    private static IDisposable EncodeJwsPayload(
        ReadOnlySpan<byte> payload,
        IBufferWriter<char> tokenWriter,
        JoseSigningOptions signingOptions,
        out ReadOnlySpan<char> encodedPayloadPart)
    {
        var payloadCharCount = signingOptions.EncodePayload ?
            Base64Url.GetCharCountForEncode(payload.Length) :
            SecureEncoding.Utf8.GetCharCount(payload);

        IDisposable lease;
        Span<char> encodedPayload;

        if (signingOptions.DetachPayload)
        {
            var payloadLease = MemoryPool<char>.Shared.Rent(payloadCharCount);
            encodedPayload = payloadLease.Memory.Span[..payloadCharCount];
            lease = payloadLease;
        }
        else
        {
            encodedPayload = tokenWriter.GetSpan(payloadCharCount + 1);
            lease = EmptyDisposable.Singleton;
        }

        try
        {
            var encodeResult = TryEncodeJose(signingOptions.EncodePayload, payload, encodedPayload, out var payloadCharsWritten);
            Debug.Assert(encodeResult && payloadCharsWritten == payloadCharCount);

            if (signingOptions.DetachPayload)
            {
                var span = tokenWriter.GetSpan(1);
                span[0] = '.';
                tokenWriter.Advance(1);
            }
            else
            {
                encodedPayload[payloadCharsWritten] = '.';
                tokenWriter.Advance(payloadCharsWritten + 1);
            }

            encodedPayloadPart = encodedPayload[..payloadCharsWritten]; // without dot
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }

    private static void EncodeJwsSignature(
        SecretKey secretKey,
        SignatureAlgorithm signatureAlgorithm,
        ReadOnlySpan<char> encodedHeaderPart,
        ReadOnlySpan<char> encodedPayloadPart,
        IBufferWriter<char> tokenWriter)
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
        using var inputLease = CryptoPool.Rent(inputByteCount, isSensitive: false, out Span<byte> inputData);

        var headerWritten = Encoding.ASCII.GetBytes(encodedHeaderPart, inputData);
        Debug.Assert(headerWritten == headerByteCount);

        var payloadWritten = Encoding.ASCII.GetBytes(encodedPayloadPart, inputData[headerByteCount..]);
        Debug.Assert(payloadWritten == payloadByteCount);

        using var signatureLease = CryptoPool.Rent(signatureByteCount, isSensitive: false, out Span<byte> signatureBytes);
        var signResult = signatureAlgorithm.TrySign(secretKey, inputData, signatureBytes, out var signatureBytesWritten);
        Debug.Assert(signResult && signatureBytesWritten == signatureByteCount);

        var signatureCharCount = Base64Url.GetCharCountForEncode(signatureByteCount);
        var encodedSignature = tokenWriter.GetSpan(signatureCharCount);

        var encodeResult = Base64Url.TryEncode(signatureBytes, encodedSignature, out var signatureCharsWritten);
        Debug.Assert(encodeResult && signatureCharsWritten == signatureCharCount);

        tokenWriter.Advance(signatureCharsWritten);
    }
}
