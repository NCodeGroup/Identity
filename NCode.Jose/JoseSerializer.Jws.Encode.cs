using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NCode.Cryptography.Keys;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Extensions;
using NCode.Jose.Internal;
using NCode.Jose.Signature;
using Nerdbank.Streams;

namespace NCode.Jose;

partial class JoseSerializer
{
    internal string EncodeJws<T>(
        T payload,
        SecretKey secretKey,
        ISignatureAlgorithm signatureAlgorithm,
        IReadOnlyDictionary<string, object>? extraHeaders = null,
        JwsOptions? options = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        using var payloadBuffer = new Sequence<byte>(ArrayPool<byte>.Shared)
        {
            MinimumSpanLength = 1024
        };

        using var jsonWriter = new Utf8JsonWriter(payloadBuffer);
        JsonSerializer.Serialize(jsonWriter, payload, JoseOptions.JsonSerializerOptions);

        var payloadSequence = payloadBuffer.AsReadOnlySequence;
        if (payloadSequence.IsSingleSegment)
        {
            var span = payloadSequence.FirstSpan;
            EncodeJws(span, secretKey, signatureAlgorithm, tokenBuffer, extraHeaders, options);
        }
        else
        {
            var payloadByteCount = (int)payloadSequence.Length;
            using var lease = CryptoPool.Rent(payloadByteCount, isSensitive: false, out Span<byte> span);
            payloadSequence.CopyTo(span);

            EncodeJws(span, secretKey, signatureAlgorithm, tokenBuffer, extraHeaders, options);
        }

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    internal void EncodeJws(
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        ISignatureAlgorithm signatureAlgorithm,
        IBufferWriter<char> tokenWriter,
        IReadOnlyDictionary<string, object>? extraHeaders = null,
        JwsOptions? options = null)
    {
        var nonNullOptions = options ?? JwsOptions.Default;

        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */

        // BASE64URL(UTF8(JWS Protected Header)) || '.'
        EncodeJwsHeader(
            signatureAlgorithm.Code,
            secretKey.KeyId,
            tokenWriter,
            extraHeaders,
            nonNullOptions,
            out var encodedHeaderPart);

        // BASE64URL(JWS Payload) || '.'
        using var _ = EncodeJwsPayload(
            payload,
            tokenWriter,
            nonNullOptions,
            out var encodedPayloadPart);

        // BASE64URL(JWS Signature)
        EncodeJwsSignature(secretKey,
            signatureAlgorithm,
            encodedHeaderPart, encodedPayloadPart, tokenWriter);
    }

    private void EncodeJwsHeader(
        string algorithmCode,
        string keyId,
        IBufferWriter<char> tokenWriter,
        IReadOnlyDictionary<string, object>? extraHeaders,
        JwsOptions options,
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

        if (!options.EncodePayload)
        {
            var crit = new HashSet<string> { "b64" };
            if (header.TryGetValue<IEnumerable<string>>("crit", out var existing))
            {
                crit.UnionWith(existing);
            }

            header["b64"] = false;
            header["crit"] = crit;
        }

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

    private static IDisposable EncodeJwsPayload(
        ReadOnlySpan<byte> payload,
        IBufferWriter<char> tokenWriter,
        JwsOptions options,
        out Span<char> encodedPayloadPart)
    {
        var payloadCharCount = options.EncodePayload ?
            Base64Url.GetCharCountForEncode(payload.Length) :
            Encoding.UTF8.GetCharCount(payload);

        IDisposable lease;
        Span<char> encodedPayload;

        if (options.DetachPayload)
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
            int payloadCharsWritten;
            if (options.EncodePayload)
            {
                var encodeResult = Base64Url.TryEncode(payload, encodedPayload, out payloadCharsWritten);
                Debug.Assert(encodeResult && payloadCharsWritten == payloadCharCount);
            }
            else
            {
                payloadCharsWritten = Encoding.UTF8.GetChars(payload, encodedPayload);
                Debug.Assert(payloadCharsWritten == payloadCharCount);
            }

            if (options.DetachPayload)
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
        ISignatureAlgorithm signatureAlgorithm,
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
