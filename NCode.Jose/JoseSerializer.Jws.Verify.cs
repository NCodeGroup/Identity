using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NCode.Cryptography.Keys;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using Nerdbank.Streams;

namespace NCode.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload, out IReadOnlyDictionary<string, object> header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compact">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(CompactToken compact, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, string detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(string token, SecretKey secretKey, string detachedPayload, out IReadOnlyDictionary<string, object> header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compact">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(CompactToken compact, SecretKey secretKey, string detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload, out IReadOnlyDictionary<string, object> header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compact">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    void VerifyJws(CompactToken compact, SecretKey secretKey, ReadOnlySpan<char> detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload, out IReadOnlyDictionary<string, object> header);

    /// <summary>
    /// Validates a JWS protected Json Web Token (JWT) with a detached payload.
    /// This method does not support JWE (i.e. encrypted) tokens.
    /// </summary>
    /// <param name="compact">The parsed JWT in compact form to validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation.</param>
    /// <param name="detachedPayload">The detached payload to validate.</param>
    /// <typeparam name="T">The type of the payload to validate.</typeparam>
    void VerifyJws<T>(CompactToken compact, SecretKey secretKey, T detachedPayload);
}

partial class JoseSerializer
{
    private static void AssertJwsDetached(CompactToken compact)
    {
        if (compact.ProtectionType != JoseConstants.JWS)
            throw new InvalidOperationException("Only JWS tokens can be validated with a detached payload.");
    }

    /// <inheritdoc />
    public void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload) =>
        VerifyJws(ParseCompact(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload, out IReadOnlyDictionary<string, object> header)
    {
        var compact = ParseCompact(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws<T>(CompactToken compact, SecretKey secretKey, T detachedPayload)
    {
        AssertJwsDetached(compact);

        using var payloadBuffer = new Sequence<byte>
        {
            MinimumSpanLength = 1024
        };

        using var jsonWriter = new Utf8JsonWriter(payloadBuffer);
        JsonSerializer.Serialize(jsonWriter, detachedPayload, JoseOptions.JsonSerializerOptions);

        var payloadSequence = payloadBuffer.AsReadOnlySequence;
        if (payloadSequence.IsSingleSegment)
        {
            var span = payloadSequence.FirstSpan;

            VerifyJws(compact, secretKey, span);
        }
        else
        {
            var payloadByteCount = (int)payloadSequence.Length;
            using var lease = CryptoPool.Rent(payloadByteCount, isSensitive: false, out Span<byte> span);
            payloadSequence.CopyTo(span);

            VerifyJws(compact, secretKey, span);
        }
    }

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, string detachedPayload) =>
        VerifyJws(ParseCompact(token), secretKey, detachedPayload.AsSpan());

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, string detachedPayload, out IReadOnlyDictionary<string, object> header) =>
        VerifyJws(token, secretKey, detachedPayload.AsSpan(), out header);

    /// <inheritdoc />
    public void VerifyJws(CompactToken compact, SecretKey secretKey, string detachedPayload) =>
        VerifyJws(compact, secretKey, detachedPayload.AsSpan());

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload) =>
        VerifyJws(ParseCompact(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload, out IReadOnlyDictionary<string, object> header)
    {
        var compact = ParseCompact(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws(CompactToken compact, SecretKey secretKey, ReadOnlySpan<char> detachedPayload)
    {
        AssertJwsDetached(compact);

        var header = compact.DeserializedHeader;
        if (!header.TryGetValue<bool>("b64", out var b64))
        {
            b64 = true;
        }

        if (b64)
        {
            var byteCount = Encoding.UTF8.GetByteCount(detachedPayload);
            using var lease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
            var bytesWritten = Encoding.UTF8.GetBytes(detachedPayload, payloadBytes);
            Debug.Assert(bytesWritten == byteCount);

            VerifyJws(compact, secretKey, payloadBytes);
        }
        else
        {
            // JWS Protected Header
            var jwsProtectedHeader = compact.Segments.First;
            var encodedHeader = compact.EncodedHeader;

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
        VerifyJws(ParseCompact(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload, out IReadOnlyDictionary<string, object> header)
    {
        var compact = ParseCompact(token);
        VerifyJws(compact, secretKey, detachedPayload);
        header = compact.DeserializedHeader;
    }

    /// <inheritdoc />
    public void VerifyJws(CompactToken compact, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload)
    {
        AssertJwsDetached(compact);

        // JWS Protected Header
        var jwsProtectedHeader = compact.Segments.First;
        var encodedHeader = compact.EncodedHeader;

        var header = compact.DeserializedHeader;
        if (!header.TryGetValue<bool>("b64", out var b64))
        {
            b64 = true;
        }

        // JWS Payload
        var jwsPayload = jwsProtectedHeader.Next!;
        using var _ = EncodeDetachedPayload(b64, detachedPayload, out var encodedPayload);

        // JWS Signature
        var jwsSignature = jwsPayload.Next!;
        var encodedSignature = jwsSignature.Memory.Span;

        VerifyJws(secretKey, header, encodedHeader, encodedPayload, encodedSignature);
    }

    private static IDisposable EncodeDetachedPayload(
        bool b64,
        ReadOnlySpan<byte> payload,
        out ReadOnlySpan<char> encodedPayload)
    {
        var charCount = b64 ?
            Base64Url.GetCharCountForEncode(payload.Length) :
            Encoding.UTF8.GetCharCount(payload);

        var lease = MemoryPool<char>.Shared.Rent(charCount);
        try
        {
            var span = lease.Memory.Span[..charCount];

            if (b64)
            {
                var encodeResult = Base64Url.TryEncode(payload, span, out var charsWritten);
                Debug.Assert(encodeResult && charsWritten == charCount);
            }
            else
            {
                var charsWritten = Encoding.UTF8.GetChars(payload, span);
                Debug.Assert(charsWritten == charCount);
            }

            encodedPayload = span;
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }

    private void VerifyJws(
        SecretKey secretKey,
        IReadOnlyDictionary<string, object> header,
        ReadOnlySpan<char> encodedHeader,
        ReadOnlySpan<char> encodedPayload,
        ReadOnlySpan<char> encodedSignature)
    {
        if (!header.TryGetValue<string>("alg", out var signatureAlgorithmCode))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
        }

        if (!AlgorithmProvider.TryGetSignatureAlgorithm(signatureAlgorithmCode, out var signatureAlgorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered signature algorithm for `{signatureAlgorithmCode}` was found.");
        }

        using var signatureLease = DecodeBase64Url(
            "JWS Signature",
            encodedSignature,
            isSensitive: false,
            out var signature);

        var expectedSignatureSizeBytes = signatureAlgorithm.GetSignatureSizeBytes(secretKey.KeySizeBits);
        if (signature.Length != expectedSignatureSizeBytes)
            throw new IntegrityJoseException($"Invalid signature size, expected {expectedSignatureSizeBytes} bytes but was {signature.Length} bytes.");

        using var signatureInputLease = GetSignatureInput(
            encodedHeader,
            encodedPayload,
            out var signatureInput);

        if (!signatureAlgorithm.Verify(secretKey, signatureInput, signature))
            throw new IntegrityJoseException("Invalid signature, verification failed.");
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
