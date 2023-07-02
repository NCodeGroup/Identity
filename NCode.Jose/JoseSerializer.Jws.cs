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
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;

namespace NCode.Jose;

partial class JoseSerializer
{
    private string DecodeJws(
        StringSegments segments,
        ISecretKeyCollection secretKeys,
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
        var localHeader = DeserializeUtf8JsonAfterBase64Url<Dictionary<string, object>>(
            "JWS Protected Header",
            jwsProtectedHeader.Memory.Span);

        if (!localHeader.TryGetValue<string>("alg", out var algorithmCode))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
        }

        if (!localHeader.TryGetValue<bool>("b64", out var b64))
        {
            b64 = true;
        }

        // JWS Payload
        var jwsPayload = jwsProtectedHeader.Next!;
        using var payloadLease = DecodePayload(
            b64,
            jwsPayload.Memory.Span,
            out var payloadBytes);

        // JWS Signature
        var jwsSignature = jwsPayload.Next!;
        using var signatureLease = DecodeBase64Url(
            "JWS Signature",
            jwsSignature.Memory.Span,
            isSensitive: false,
            out var signature);

        if (algorithmCode == AlgorithmCodes.DigitalSignature.None)
        {
            if (signature.Length != 0)
            {
                throw new IntegrityJoseException("Signature validation failed, expected no signature but was present.");
            }

            var payload = Encoding.UTF8.GetString(payloadBytes);
            header = localHeader;
            return payload;
        }

        if (!AlgorithmProvider.TryGetSignatureAlgorithm(algorithmCode, out var algorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered signing algorithm for `{algorithmCode}` was found.");
        }

        using var signatureActualLease = GetSignatureInput(
            jwsProtectedHeader.Memory.Span,
            jwsPayload.Memory.Span,
            out var signatureInput);

        if (localHeader.TryGetValue<string>("kid", out var keyId) &&
            secretKeys.TryGetByKeyId(keyId, out var specificKey) &&
            algorithm.Verify(specificKey, signatureInput, signature))
        {
            var payload = Encoding.UTF8.GetString(payloadBytes);
            header = localHeader;
            return payload;
        }

        // ReSharper disable once LoopCanBeConvertedToQuery
        // Nope, we can't do that when using byref span
        foreach (var secretKey in secretKeys)
        {
            if (!algorithm.Verify(secretKey, signatureInput, signature)) continue;
            var payload = Encoding.UTF8.GetString(payloadBytes);
            header = localHeader;
            return payload;
        }

        throw new IntegrityJoseException("Signature validation failed, no matching signing key.");
    }

    private static IMemoryOwner<byte> DecodePayload(bool b64, ReadOnlySpan<char> encodedPayload, out Span<byte> payload)
    {
        var byteCount = b64 ? Base64Url.GetByteCountForDecode(encodedPayload.Length) : Encoding.UTF8.GetByteCount(encodedPayload);
        var lease = RentBuffer(byteCount, isSensitive: false, out payload);
        try
        {
            if (b64)
            {
                var result = Base64Url.TryDecode(encodedPayload, payload, out var bytesWritten);
                Debug.Assert(result && bytesWritten == byteCount);
            }
            else
            {
                var bytesWritten = Encoding.UTF8.GetBytes(encodedPayload, payload);
                Debug.Assert(bytesWritten == byteCount);
            }
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }

    private static IMemoryOwner<byte> GetSignatureInput(
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        out Span<byte> signatureInput)
    {
        var headerByteCount = Encoding.UTF8.GetByteCount(encodedHeader);
        var payloadByteCount = Encoding.UTF8.GetByteCount(encodedPayload);
        var totalByteCount = headerByteCount + 1 + payloadByteCount;
        var lease = RentBuffer(totalByteCount, isSensitive: false, out signatureInput);
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
