// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Json;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private static void AssertJwsDetached(CompactJwt compactJwt)
    {
        if (compactJwt.ProtectionType != JoseProtectionTypes.Jws)
            throw new InvalidOperationException(
                "Only JWS tokens can be validated with a detached payload."
            );
    }

    private SignatureAlgorithm GetSignatureAlgorithm(string code) =>
        !AlgorithmCollection.TryGetSignatureAlgorithm(code, out var algorithm)
            ? throw new JoseInvalidAlgorithmException(
                $"The `{code}` algorithm is not supported for digital signatures."
            )
            : AssertEnabled(algorithm);

    private void VerifyJwsCompact(
        CompactJwt compactJwt,
        SecretKey secretKey,
        ReadOnlySpan<byte> detachedPayload
    )
    {
        AssertJwsDetached(compactJwt);

        var segments = compactJwt.Segments;
        if (segments.Count != JwsSegmentCount)
            throw new InvalidOperationException("Malformed JWS token.");

        var header = compactJwt.Header;
        var encodedHeader = segments[0].Span;
        var encodedSignature = segments[2].Span;

        if (!header.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64))
        {
            b64 = true;
        }

        using var payloadLease = EncodeJose(b64, detachedPayload, out var encodedPayload);

        VerifyJws(secretKey, header, encodedHeader, encodedPayload, encodedSignature);
    }

    private void VerifyJws(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        ReadOnlySpan<char> encodedSignature
    )
    {
        if (
            !header.TryGetPropertyValue<string>(
                JoseClaimNames.Header.Alg,
                out var signatureAlgorithmCode
            )
        )
            throw new JoseException("The JWT header is missing the 'alg' field.");

        var signatureAlgorithm = GetSignatureAlgorithm(signatureAlgorithmCode);

        using var signatureLease = DecodeBase64Url(
            encodedSignature,
            isSensitive: false,
            out var signature
        );

        var expectedSignatureSizeBytes = signatureAlgorithm.GetSignatureSizeBytes(
            secretKey.KeySizeBits
        );
        if (signature.Length != expectedSignatureSizeBytes)
            throw new JoseIntegrityException(
                $"Invalid signature size, expected {expectedSignatureSizeBytes} bytes but was {signature.Length} bytes."
            );

        using var signatureInputLease = GetSignatureInput(
            encodedHeader,
            encodedPayload,
            out var signatureInput
        );

        if (!signatureAlgorithm.Verify(secretKey, signatureInput, signature))
            throw new JoseIntegrityException("Invalid signature, verification failed.");
    }

    private static IDisposable GetSignatureInput(
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        out ReadOnlySpan<byte> signatureInput
    )
    {
        var headerByteCount = SecureEncoding.UTF8.GetByteCount(encodedHeader);
        var payloadByteCount = SecureEncoding.UTF8.GetByteCount(encodedPayload);
        var totalByteCount = headerByteCount + 1 + payloadByteCount;
        var lease = BufferFactory.Rent(totalByteCount, isSensitive: false, out Span<byte> span);
        try
        {
            var bytesRead = SecureEncoding.UTF8.GetBytes(encodedHeader, span);
            Debug.Assert(bytesRead == headerByteCount);

            span[headerByteCount] = (byte)'.';

            bytesRead = SecureEncoding.UTF8.GetBytes(encodedPayload, span[(headerByteCount + 1)..]);
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
