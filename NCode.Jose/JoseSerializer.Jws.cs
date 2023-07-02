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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

        // TODO: should we use CryptoPool?

        // JWS Protected Header
        var jwsProtectedHeader = segments.First;
        var headerBytes = Base64Url.Decode(jwsProtectedHeader.Memory.Span);
        var localHeader = DeserializeUtf8Json<Dictionary<string, object>>("JWS Protected Header", headerBytes);

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
        var payloadBytes = DecodePayload(b64, jwsPayload.Memory.Span);

        // JWS Signature
        var jwsSignature = jwsPayload.Next!;
        var expectedSignature = Base64Url.Decode(jwsSignature.Memory.Span);

        if (algorithmCode == AlgorithmCodes.DigitalSignature.None)
        {
            if (expectedSignature.Length != 0)
            {
                throw new IntegrityJoseException("Signature validation failed, expected no signature but was present.");
            }

            header = localHeader;
            return Encoding.UTF8.GetString(payloadBytes);
        }

        if (!AlgorithmProvider.TryGetSignatureAlgorithm(algorithmCode, out var algorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered signing algorithm for `{algorithmCode}` was found.");
        }

        var signatureInput = GetSignatureInput(jwsProtectedHeader.Memory.Span, jwsPayload.Memory.Span);

        bool TryVerify(SecretKey secretKey, [MaybeNullWhen(false)] out string payload)
        {
            if (algorithm.Verify(secretKey, signatureInput, expectedSignature))
            {
                payload = Encoding.UTF8.GetString(payloadBytes);
                return true;
            }

            payload = null;
            return false;
        }

        var payload = string.Empty;
        if (localHeader.TryGetValue<string>("kid", out var keyId) &&
            secretKeys.TryGetByKeyId(keyId, out var specificKey) &&
            TryVerify(specificKey, out payload))
        {
            header = localHeader;
            return payload;
        }

        if (!secretKeys.Any(secretKey => TryVerify(secretKey, out payload)))
        {
            throw new IntegrityJoseException("Signature validation failed, no matching signing key.");
        }

        Debug.Assert(payload != null);
        header = localHeader;
        return payload;
    }

    private static byte[] GetSignatureInput(ReadOnlySpan<char> encodedHeader, ReadOnlySpan<char> encodedPayload)
    {
        var headerByteCount = Encoding.UTF8.GetByteCount(encodedHeader);
        var payloadByteCount = Encoding.UTF8.GetByteCount(encodedPayload);

        var result = new byte[headerByteCount + 1 + payloadByteCount];

        Encoding.UTF8.GetBytes(encodedHeader, result);
        result[headerByteCount] = (byte)'.';
        Encoding.UTF8.GetBytes(encodedPayload, result.AsSpan(headerByteCount + 1));

        return result;
    }
}
