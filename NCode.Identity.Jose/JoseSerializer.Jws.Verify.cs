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
using System.Text.Json;
using NCode.CryptoMemory;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Jose.Json;
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose;

// TODO: breakout the verify overloads into extension methods

partial class JoseSerializer
{
    private static void AssertJwsDetached(CompactJwt compactJwt)
    {
        if (compactJwt.ProtectionType != JoseProtectionTypes.Jws)
            throw new InvalidOperationException("Only JWS tokens can be validated with a detached payload.");
    }

    private SignatureAlgorithm GetSignatureAlgorithm(string code) =>
        !AlgorithmCollection.TryGetSignatureAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"The `{code}` algorithm is not supported for digital signatures.") :
            algorithm;

    /// <inheritdoc />
    public void VerifyJws<T>(
        string token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions) =>
        VerifyJws(ParseCompactJwt(token), secretKey, detachedPayload, jsonOptions);

    /// <inheritdoc />
    public void VerifyJws<T>(
        string token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions,
        out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        VerifyJws(compact, secretKey, detachedPayload, jsonOptions);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws<T>(
        CompactJwt compactJwt,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions)
    {
        AssertJwsDetached(compactJwt);
        using var _ = SerializeToUtf8(detachedPayload, jsonOptions, out var bytes);
        VerifyJws(compactJwt, secretKey, bytes);
    }

    /// <inheritdoc />
    public void VerifyJws(
        string token,
        SecretKey secretKey,
        string detachedPayload) =>
        VerifyJws(
            ParseCompactJwt(token),
            secretKey,
            detachedPayload.AsSpan());

    /// <inheritdoc />
    public void VerifyJws(
        string token,
        SecretKey secretKey,
        string detachedPayload,
        out JsonElement header) =>
        VerifyJws(
            token,
            secretKey,
            detachedPayload.AsSpan(),
            out header);

    /// <inheritdoc />
    public void VerifyJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        string detachedPayload) =>
        VerifyJws(
            compactJwt,
            secretKey,
            detachedPayload.AsSpan());

    /// <inheritdoc />
    public void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<char> detachedPayload) =>
        VerifyJws(
            ParseCompactJwt(token),
            secretKey,
            detachedPayload);

    /// <inheritdoc />
    public void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<char> detachedPayload,
        out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        ReadOnlySpan<char> detachedPayload)
    {
        AssertJwsDetached(compactJwt);

        var header = compactJwt.DeserializedHeader;
        if (!header.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64))
        {
            b64 = true;
        }

        if (b64)
        {
            var byteCount = SecureEncoding.Utf8.GetByteCount(detachedPayload);
            using var lease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
            var bytesWritten = SecureEncoding.Utf8.GetBytes(detachedPayload, payloadBytes);
            Debug.Assert(bytesWritten == byteCount);

            VerifyJws(compactJwt, secretKey, payloadBytes);
        }
        else
        {
            // JWS Protected Header
            var jwsProtectedHeader = compactJwt.Segments.First;
            var encodedHeader = compactJwt.EncodedHeader;

            // JWS Payload
            var jwsPayload = jwsProtectedHeader.Next!;

            // JWS Signature
            var jwsSignature = jwsPayload.Next!;
            var encodedSignature = jwsSignature.Memory.Span;

            VerifyJws(secretKey, header, encodedHeader, detachedPayload, encodedSignature);
        }
    }

    /// <inheritdoc />
    public void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload) =>
        VerifyJws(
            ParseCompactJwt(token),
            secretKey,
            detachedPayload);

    /// <inheritdoc />
    public void VerifyJws(
        string token,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload,
        out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload)
    {
        AssertJwsDetached(compactJwt);

        // JWS Protected Header
        var jwsProtectedHeader = compactJwt.Segments.First;
        var encodedHeader = compactJwt.EncodedHeader;

        var header = compactJwt.DeserializedHeader;
        if (!header.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64))
        {
            b64 = true;
        }

        // JWS Payload
        var jwsPayload = jwsProtectedHeader.Next!;
        using var payloadLease = EncodeJose(b64, detachedPayload, out var encodedPayload);

        // JWS Signature
        var jwsSignature = jwsPayload.Next!;
        var encodedSignature = jwsSignature.Memory.Span;

        VerifyJws(secretKey, header, encodedHeader, encodedPayload, encodedSignature);
    }

    private void VerifyJws(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        ReadOnlySpan<char> encodedSignature)
    {
        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var signatureAlgorithmCode))
            throw new JoseException("The JWT header is missing the 'alg' field.");

        var signatureAlgorithm = GetSignatureAlgorithm(signatureAlgorithmCode);

        using var signatureLease = DecodeBase64Url(
            encodedSignature,
            isSensitive: false,
            out var signature);

        var expectedSignatureSizeBytes = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        if (signature.Length != expectedSignatureSizeBytes)
            throw new JoseIntegrityException($"Invalid signature size, expected {expectedSignatureSizeBytes} bytes but was {signature.Length} bytes.");

        using var signatureInputLease = GetSignatureInput(
            encodedHeader,
            encodedPayload,
            out var signatureInput);

        if (!signatureAlgorithm.Verify(secretKey, signatureInput, signature))
            throw new JoseIntegrityException("Invalid signature, verification failed.");
    }

    private static IDisposable GetSignatureInput(
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        out ReadOnlySpan<byte> signatureInput)
    {
        var headerByteCount = SecureEncoding.Utf8.GetByteCount(encodedHeader);
        var payloadByteCount = SecureEncoding.Utf8.GetByteCount(encodedPayload);
        var totalByteCount = headerByteCount + 1 + payloadByteCount;
        var lease = CryptoPool.Rent(totalByteCount, isSensitive: false, out Span<byte> span);
        try
        {
            var bytesRead = SecureEncoding.Utf8.GetBytes(encodedHeader, span);
            Debug.Assert(bytesRead == headerByteCount);

            span[headerByteCount] = (byte)'.';

            bytesRead = SecureEncoding.Utf8.GetBytes(encodedPayload, span[(headerByteCount + 1)..]);
            Debug.Assert(bytesRead == payloadByteCount);
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        signatureInput = span;
        return lease;
    }
}
