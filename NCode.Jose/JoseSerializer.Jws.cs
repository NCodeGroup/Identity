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
using NCode.Cryptography.Keys;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using NCode.Jose.Signature;
using Nerdbank.Streams;

namespace NCode.Jose;

partial class JoseSerializer
{
    internal string EncodeJws<T>(
        T payload,
        SecretKey secretKey,
        ISignatureAlgorithm signatureAlgorithm,
        IReadOnlyDictionary<string, object>? extraHeaders = null)
    {
        using var payloadBytes = new Sequence<byte>(ArrayPool<byte>.Shared);
        using var jsonWriter = new Utf8JsonWriter(payloadBytes);
        JsonSerializer.Serialize(jsonWriter, payload, JoseOptions.JsonSerializerOptions);

        using var tokenWriter = new Sequence<char>(ArrayPool<char>.Shared);
        EncodeJws(payloadBytes, secretKey, signatureAlgorithm, tokenWriter, extraHeaders);

        return tokenWriter.AsReadOnlySequence.ToString();
    }

    internal void EncodeJws(
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        ISignatureAlgorithm signatureAlgorithm,
        IBufferWriter<char> tokenWriter,
        IReadOnlyDictionary<string, object>? extraHeaders = null)
    {
        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */

        var signatureByteCount = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        var isEmptySignature = signatureByteCount == 0;

        // BASE64URL(UTF8(JWS Protected Header)) || '.'
        EncodeJwsHeader(
            signatureAlgorithm.Code,
            secretKey.KeyId,
            tokenWriter,
            extraHeaders,
            out var encodedHeaderPart);

        // BASE64URL(JWS Payload) || '.'
        var payloadByteCount = payload.Length;
        var payloadCharCount = Base64Url.GetCharCountForEncode(payloadByteCount);
        var encodedPayload = tokenWriter.GetSpan(payloadCharCount + 1);
        var encodePayloadResult = Base64Url.TryEncode(payload, encodedPayload, out var payloadCharsWritten);
        Debug.Assert(encodePayloadResult && payloadCharsWritten == payloadCharCount);
        encodedPayload[payloadCharsWritten] = '.';
        tokenWriter.Advance(payloadCharsWritten + 1);

        if (isEmptySignature)
            return;

        var encodedPayloadPart = encodedPayload[..payloadCharsWritten]; // without dot

        // BASE64URL(JWS Signature)
        EncodeJwsSignature(
            encodedHeaderPart,
            encodedPayloadPart,
            secretKey,
            signatureAlgorithm,
            tokenWriter);
    }

    internal void EncodeJws(
        ReadOnlySequence<byte> payload,
        SecretKey secretKey,
        ISignatureAlgorithm signatureAlgorithm,
        IBufferWriter<char> tokenWriter,
        IReadOnlyDictionary<string, object>? extraHeaders = null)
    {
        if (payload.IsSingleSegment)
        {
            EncodeJws(payload.FirstSpan, secretKey, signatureAlgorithm, tokenWriter, extraHeaders);
            return;
        }

        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */

        var signatureByteCount = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        var isEmptySignature = signatureByteCount == 0;

        // BASE64URL(UTF8(JWS Protected Header)) || '.'
        EncodeJwsHeader(
            signatureAlgorithm.Code,
            secretKey.KeyId,
            tokenWriter,
            extraHeaders,
            out var encodedHeaderPart);

        // BASE64URL(JWS Payload) || '.'
        var payloadByteCount = (int)payload.Length;
        var payloadCharCount = Base64Url.GetCharCountForEncode(payloadByteCount);
        var encodedPayload = tokenWriter.GetSpan(payloadCharCount + 1);
        var encodePayloadResult = Base64Url.TryEncode(payload, encodedPayload, out var payloadCharsWritten);
        Debug.Assert(encodePayloadResult && payloadCharsWritten == payloadCharCount);
        encodedPayload[payloadCharsWritten] = '.';
        tokenWriter.Advance(payloadCharsWritten + 1);

        if (isEmptySignature)
            return;

        var encodedPayloadPart = encodedPayload[..payloadCharsWritten]; // without dot

        // BASE64URL(JWS Signature)
        EncodeJwsSignature(
            encodedHeaderPart,
            encodedPayloadPart,
            secretKey,
            signatureAlgorithm,
            tokenWriter);
    }

    private void EncodeJwsHeader(
        string algorithmCode,
        string keyId,
        IBufferWriter<char> tokenWriter,
        IReadOnlyDictionary<string, object>? extraHeaders,
        out Span<char> encodedHeaderPart)
    {
        var header = extraHeaders != null ?
            new Dictionary<string, object>(extraHeaders) :
            new Dictionary<string, object>();

        header.TryAdd("typ", "JWT");

        if (!string.IsNullOrEmpty(algorithmCode))
            header["alg"] = algorithmCode;

        if (!string.IsNullOrEmpty(keyId))
            header["kid"] = keyId;

        using var headerSequence = new Sequence<byte>(ArrayPool<byte>.Shared);
        using var headerWriter = new Utf8JsonWriter(headerSequence);
        JsonSerializer.Serialize(headerWriter, header, JoseOptions.JsonSerializerOptions);

        var headerByteCount = (int)headerSequence.Length;
        var headerCharCount = Base64Url.GetCharCountForEncode(headerByteCount);
        var encodedHeader = tokenWriter.GetSpan(headerCharCount + 1);

        var encodeHeaderResult = Base64Url.TryEncode(headerSequence, encodedHeader, out var headerCharsWritten);
        Debug.Assert(encodeHeaderResult && headerCharsWritten == headerCharCount);

        encodedHeader[headerCharsWritten] = '.';
        tokenWriter.Advance(headerCharsWritten + 1);

        encodedHeaderPart = encodedHeader[..(headerCharsWritten + 1)]; // with dot
    }

    private static void EncodeJwsSignature(
        ReadOnlySpan<char> encodedHeaderPart,
        ReadOnlySpan<char> encodedPayloadPart,
        SecretKey secretKey,
        ISignatureAlgorithm signatureAlgorithm,
        IBufferWriter<char> tokenWriter)
    {
        // Input Data for Signature
        // UTF8( BASE64URL(UTF8(JWS Protected Header)) || '.' || BASE64URL(JWS Payload) )

        var headerByteCount = Encoding.UTF8.GetByteCount(encodedHeaderPart);
        var payloadByteCount = Encoding.UTF8.GetByteCount(encodedPayloadPart);

        var inputByteCount = headerByteCount + payloadByteCount;
        using var inputLease = CryptoPool.Rent(inputByteCount, isSensitive: false, out Span<byte> inputData);

        var headerWritten = Encoding.UTF8.GetBytes(encodedHeaderPart, inputData);
        Debug.Assert(headerWritten == headerByteCount);

        var payloadWritten = Encoding.UTF8.GetBytes(encodedPayloadPart, inputData[headerByteCount..]);
        Debug.Assert(payloadWritten == payloadByteCount);

        var signatureByteCount = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        using var signatureLease = CryptoPool.Rent(signatureByteCount, isSensitive: false, out Span<byte> signatureBytes);

        var signResult = signatureAlgorithm.TrySign(secretKey, inputData, signatureBytes, out var signatureBytesWritten);
        Debug.Assert(signResult && signatureBytesWritten == signatureByteCount);

        var signatureCharCount = Base64Url.GetCharCountForEncode(signatureByteCount);
        var encodedSignature = tokenWriter.GetSpan(signatureCharCount);

        var encodeResult = Base64Url.TryEncode(signatureBytes, encodedSignature, out var signatureCharsWritten);
        Debug.Assert(encodeResult && signatureCharsWritten == signatureCharCount);

        tokenWriter.Advance(signatureCharsWritten);
    }

    private string DecodeJws(CompactToken compactToken, SecretKey secretKey)
    {
        using var byteSequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        DecodeJws(compactToken, secretKey, byteSequence);

        return DecodeUtf8(byteSequence);
    }

    private T? DeserializeJws<T>(CompactToken compactToken, SecretKey secretKey)
    {
        using var byteSequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        DecodeJws(compactToken, secretKey, byteSequence);

        return Deserialize<T>(byteSequence);
    }

    private void DecodeJws(CompactToken compactToken, SecretKey secretKey, IBufferWriter<byte> payloadWriter)
    {
        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */
        Debug.Assert(compactToken.ProtectionType == JoseConstants.JWS);

        // JWS Protected Header
        var jwsProtectedHeader = compactToken.Segments.First;
        var header = compactToken.DeserializedHeader;

        if (!header.TryGetValue<string>("alg", out var signatureAlgorithmCode))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
        }

        if (!AlgorithmProvider.TryGetSignatureAlgorithm(signatureAlgorithmCode, out var signatureAlgorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered signature algorithm for `{signatureAlgorithmCode}` was found.");
        }

        // JWS Payload
        var jwsPayload = jwsProtectedHeader.Next!;
        var encodedPayload = jwsPayload.Memory.Span;

        // JWS Signature
        var jwsSignature = jwsPayload.Next!;
        var encodedSignature = jwsSignature.Memory.Span;
        using var signatureLease = DecodeBase64Url(
            "JWS Signature",
            encodedSignature,
            isSensitive: false,
            out var signature);

        var expectedSignatureSizeBytes = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        if (signature.Length != expectedSignatureSizeBytes)
            throw new IntegrityJoseException($"Invalid signature size, expected {expectedSignatureSizeBytes} bytes but was {signature.Length} bytes.");

        using var signatureInputLease = GetSignatureInput(
            compactToken.EncodedHeader,
            encodedPayload,
            out var signatureInput);

        if (!signatureAlgorithm.Verify(secretKey, signatureInput, signature))
            throw new IntegrityJoseException("Invalid signature, verification failed.");

        if (!header.TryGetValue<bool>("b64", out var b64))
        {
            b64 = true;
        }

        if (b64)
        {
            Base64Url.Decode(encodedPayload, payloadWriter);
        }
        else
        {
            Encoding.UTF8.GetBytes(encodedPayload, payloadWriter);
        }
    }

    /// <summary>
    /// Get the JWT input data that is used to sign and verify digital signatures.
    /// </summary>
    /// <param name="encodedHeader">Contains the encoded header that was signed in the JWT.</param>
    /// <param name="encodedPayload">Contains the encoded payload that was signed in the JWT.</param>
    /// <param name="signatureInput">The byte array to receive the JWT input data for digital signatures.</param>
    /// <returns></returns>
    private static IMemoryOwner<byte> GetSignatureInput(
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        out Span<byte> signatureInput)
    {
        var headerByteCount = Encoding.UTF8.GetByteCount(encodedHeader);
        var payloadByteCount = Encoding.UTF8.GetByteCount(encodedPayload);
        var totalByteCount = headerByteCount + 1 + payloadByteCount;
        var lease = CryptoPool.Rent(totalByteCount, isSensitive: false, out signatureInput);
        try
        {
            var bytesRead = Encoding.UTF8.GetBytes(encodedHeader, signatureInput);
            Debug.Assert(bytesRead == headerByteCount);

            signatureInput[headerByteCount] = (byte)'.';

            bytesRead = Encoding.UTF8.GetBytes(encodedPayload, signatureInput[(headerByteCount + 1)..]);
            Debug.Assert(bytesRead == payloadByteCount);
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }
}
