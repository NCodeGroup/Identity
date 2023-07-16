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
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using Nerdbank.Streams;

namespace NCode.Jose;

partial class JoseSerializer
{
    private string DecodeJws(
        StringSegments segments,
        ISecretKeyCollection secretKeys,
        out IReadOnlyDictionary<string, object> header)
    {
        using var byteSequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        DecodeJws(segments, secretKeys, byteSequence, out var localHeader);

        var payload = DecodeUtf8(byteSequence);

        header = localHeader;
        return payload;
    }

    private T? DeserializeJws<T>(
        StringSegments segments,
        ISecretKeyCollection secretKeys,
        JsonSerializerOptions options,
        out IReadOnlyDictionary<string, object> header)
    {
        using var byteSequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        DecodeJws(segments, secretKeys, byteSequence, out var localHeader);

        var payload = Deserialize<T>(byteSequence, options);

        header = localHeader;
        return payload;
    }

    private void DecodeJws(
        StringSegments segments,
        ISecretKeyCollection secretKeys,
        IBufferWriter<byte> payloadWriter,
        out IReadOnlyDictionary<string, object> header)
    {
        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */
        Debug.Assert(segments.Count == JwsSegmentCount);

        // JWS Protected Header
        var jwsProtectedHeader = segments.First;
        var encodedHeader = jwsProtectedHeader.Memory.Span;
        var localHeader = DeserializeHeader(
            "JWS Protected Header",
            encodedHeader);

        if (!localHeader.TryGetValue<string>("alg", out var signatureAlgorithmCode))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
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

        VerifySignature(
            secretKeys,
            encodedHeader,
            encodedPayload,
            signature,
            signatureAlgorithmCode,
            localHeader);

        if (!localHeader.TryGetValue<bool>("b64", out var b64))
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

        header = localHeader;
    }

    private void VerifySignature(
        ISecretKeyCollection secretKeys,
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        ReadOnlySpan<byte> signature,
        string signatureAlgorithmCode,
        IReadOnlyDictionary<string, object> header)
    {
        if (signatureAlgorithmCode == AlgorithmCodes.DigitalSignature.None)
        {
            if (signature.Length != 0)
            {
                throw new IntegrityJoseException("Signature validation failed, expected no signature but was present.");
            }

            return;
        }

        if (!AlgorithmProvider.TryGetSignatureAlgorithm(signatureAlgorithmCode, out var signingAlgorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered signing algorithm for `{signatureAlgorithmCode}` was found.");
        }

        using var signatureInputLease = GetSignatureInput(
            encodedHeader,
            encodedPayload,
            out var signatureInput);

        if (header.TryGetValue<string>("kid", out var keyId) &&
            secretKeys.TryGetByKeyId(keyId, out var specificKey) &&
            signingAlgorithm.Verify(specificKey, signatureInput, signature))
        {
            return;
        }

        // ReSharper disable once LoopCanBeConvertedToQuery
        // Nope, we can't do that when using byref span
        foreach (var secretKey in secretKeys)
        {
            if (signingAlgorithm.Verify(secretKey, signatureInput, signature)) return;
        }

        throw new IntegrityJoseException("Signature validation failed, no matching signing key.");
    }

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
