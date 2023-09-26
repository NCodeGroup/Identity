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
using System.Text;
using System.Text.Json;
using NCode.CryptoMemory;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.Exceptions;
using NCode.Jose.Json;
using NCode.Jose.SecretKeys;

namespace NCode.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload, out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(CompactJwt compactJwt, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, string detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(string token, SecretKey secretKey, string detachedPayload, out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(CompactJwt compactJwt, SecretKey secretKey, string detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload, out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(CompactJwt compactJwt, SecretKey secretKey, ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload, out JsonElement header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(CompactJwt compactJwt, SecretKey secretKey, T detachedPayload);
}

partial class JoseSerializer
{
    private static void AssertJwsDetached(CompactJwt compactJwt)
    {
        if (compactJwt.ProtectionType != JoseConstants.Jws)
            throw new InvalidOperationException("Only JWS tokens can be validated with a detached payload.");
    }

    private ISignatureAlgorithm GetSignatureAlgorithm(string code) =>
        !AlgorithmCollection.TryGetSignatureAlgorithm(code, out var algorithm) ?
            throw new JoseInvalidAlgorithmException($"The `{code}` algorithm is not supported for digital signatures.") :
            algorithm;

    /// <inheritdoc />
    public void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload) =>
        VerifyJws(ParseCompactJwt(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload, out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws<T>(CompactJwt compactJwt, SecretKey secretKey, T detachedPayload)
    {
        AssertJwsDetached(compactJwt);
        using var _ = Serialize(detachedPayload, out var bytes);
        VerifyJws(compactJwt, secretKey, bytes);
    }

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, string detachedPayload) =>
        VerifyJws(ParseCompactJwt(token), secretKey, detachedPayload.AsSpan());

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, string detachedPayload, out JsonElement header) =>
        VerifyJws(token, secretKey, detachedPayload.AsSpan(), out header);

    /// <inheritdoc />
    public void VerifyJws(CompactJwt compactJwt, SecretKey secretKey, string detachedPayload) =>
        VerifyJws(compactJwt, secretKey, detachedPayload.AsSpan());

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload) =>
        VerifyJws(ParseCompactJwt(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload, out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws(CompactJwt compactJwt, SecretKey secretKey, ReadOnlySpan<char> detachedPayload)
    {
        AssertJwsDetached(compactJwt);

        var header = compactJwt.DeserializedHeader;
        if (!header.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64))
        {
            b64 = true;
        }

        if (b64)
        {
            var byteCount = Encoding.UTF8.GetByteCount(detachedPayload);
            using var lease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
            var bytesWritten = Encoding.UTF8.GetBytes(detachedPayload, payloadBytes);
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
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload) =>
        VerifyJws(ParseCompactJwt(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload, out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws(CompactJwt compactJwt, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload)
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
            "JWS Signature",
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
        var headerByteCount = Encoding.UTF8.GetByteCount(encodedHeader);
        var payloadByteCount = Encoding.UTF8.GetByteCount(encodedPayload);
        var totalByteCount = headerByteCount + 1 + payloadByteCount;
        var lease = CryptoPool.Rent(totalByteCount, isSensitive: false, out Span<byte> span);
        try
        {
            var bytesRead = Encoding.UTF8.GetBytes(encodedHeader, span);
            Debug.Assert(bytesRead == headerByteCount);

            span[headerByteCount] = (byte)'.';

            bytesRead = Encoding.UTF8.GetBytes(encodedPayload, span[(headerByteCount + 1)..]);
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
