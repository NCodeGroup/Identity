// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using NCode.Buffers;
using NCode.Disposables;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Encoders;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Json;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    /// <inheritdoc />
    public JoseEncoder CreateEncoder(JoseSigningOptions signingOptions) =>
        new JoseSigningEncoder(this, signingOptions);

    internal async ValueTask EncodeAsync(
        IBufferWriter<char> tokenWriter,
        ReadOnlyMemory<byte> payload,
        JoseSigningOptions signingOptions,
        CancellationToken cancellationToken
    )
    {
        if (signingOptions.Signatures.Count == 0)
            throw new JoseException("At least one signature is required to encode a JWS token.");

        switch (signingOptions.Format)
        {
            case JoseSerializationFormat.Compact:
                await EncodeCompactJwsAsync(
                    tokenWriter,
                    payload,
                    signingOptions,
                    cancellationToken
                );
                break;

            case JoseSerializationFormat.Flattened:
            case JoseSerializationFormat.General:
                await EncodeJsonJwsAsync(tokenWriter, payload, signingOptions, cancellationToken);
                break;

            default:
                throw new JoseException(
                    $"The '{signingOptions.Format}' serialization format is not supported for JWS tokens."
                );
        }
    }

    private async ValueTask EncodeCompactJwsAsync(
        IBufferWriter<char> tokenWriter,
        ReadOnlyMemory<byte> payload,
        JoseSigningOptions signingOptions,
        CancellationToken cancellationToken
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
            payload.Span,
            tokenWriter,
            signingOptions,
            out var encodedPayloadPart
        );

        // BASE64URL(JWS Signature)
        if (signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits) == 0)
            return;

        // the signing input is complete before the signature is awaited, so no span crosses the await
        using var inputLease = RentJwsSigningInput(
            encodedHeaderPart[..^1],
            encodedPayloadPart,
            out var signingInput
        );

        var encodedSignature = await SignJwsAsync(
            secretKey,
            signatureAlgorithm,
            signingInput,
            cancellationToken
        );

        WriteCompactSegment(encodedSignature, tokenWriter, addDot: false);
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

    private static IDisposable RentJwsSigningInput(
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        out ReadOnlyMemory<byte> signingInput
    )
    {
        // Input Data for Signature
        // b64 true:  ASCII( BASE64URL(UTF8(JWS Protected Header)) || '.' || BASE64URL(JWS Payload) )
        // b64 false: ASCII( BASE64URL(UTF8(JWS Protected Header)) || '.' ) || UTF8(JWS Payload)

        var headerByteCount = SecureEncoding.ASCII.GetByteCount(encodedHeader);
        var payloadByteCount = SecureEncoding.UTF8.GetByteCount(encodedPayload);

        var inputByteCount = headerByteCount + 1 + payloadByteCount;
        var lease = BufferFactory.Rent(
            inputByteCount,
            isSensitive: false,
            out Memory<byte> inputData
        );

        try
        {
            var inputSpan = inputData.Span;

            var headerWritten = SecureEncoding.ASCII.GetBytes(encodedHeader, inputSpan);
            Debug.Assert(headerWritten == headerByteCount);

            inputSpan[headerByteCount] = (byte)'.';

            var payloadWritten = SecureEncoding.UTF8.GetBytes(
                encodedPayload,
                inputSpan[(headerByteCount + 1)..]
            );
            Debug.Assert(payloadWritten == payloadByteCount);
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        signingInput = inputData;
        return lease;
    }

    private static async ValueTask<string> SignJwsAsync(
        SecretKey secretKey,
        SignatureAlgorithm signatureAlgorithm,
        ReadOnlyMemory<byte> signingInput,
        CancellationToken cancellationToken
    )
    {
        var signatureByteCount = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        using var signatureLease = BufferFactory.Rent(
            signatureByteCount,
            isSensitive: false,
            out Memory<byte> signatureBytes
        );

        var signatureBytesWritten = await signatureAlgorithm.SignAsync(
            secretKey,
            signingInput,
            signatureBytes,
            cancellationToken
        );
        Debug.Assert(signatureBytesWritten == signatureByteCount);

        return Base64Url.Encode((ReadOnlySpan<byte>)signatureBytes.Span[..signatureBytesWritten]);
    }
}
