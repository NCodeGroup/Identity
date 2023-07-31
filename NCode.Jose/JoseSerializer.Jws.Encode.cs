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
using NCode.Cryptography.Keys;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Extensions;
using NCode.Jose.Internal;
using NCode.Jose.Signature;
using Nerdbank.Streams;

namespace NCode.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JWS token.</returns>
    string EncodeJws<T>(
        T payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    void EncodeJws<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    /// <returns>The encoded JWS token.</returns>
    string EncodeJws(
        string payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    void EncodeJws(
        IBufferWriter<char> tokenWriter,
        string payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    /// <returns>The encoded JWS token.</returns>
    string EncodeJws(
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    void EncodeJws(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    /// <returns>The encoded JWS token.</returns>
    string EncodeJws(
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for signing.</param>
    /// <param name="signatureAlgorithmCode">The <c>Code</c> of the <see cref="ISignatureAlgorithm"/> to use for signing.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    /// <param name="options">Options that specify how the JWS token is encoded.</param>
    void EncodeJws(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null);
}

partial class JoseSerializer
{
    private static JwsOptions DefaultJwsOptions { get; } = new();

    /// <inheritdoc />
    public string EncodeJws<T>(
        T payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        EncodeJws(tokenBuffer, payload, secretKey, signatureAlgorithmCode, extraHeaders, options);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void EncodeJws<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        using var _ = Serialize(payload, out var bytes);
        EncodeJws(tokenWriter, bytes, secretKey, signatureAlgorithmCode, extraHeaders, options);
    }

    /// <inheritdoc />
    public string EncodeJws(
        string payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        EncodeJws(tokenBuffer, payload, secretKey, signatureAlgorithmCode, extraHeaders, options);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void EncodeJws(
        IBufferWriter<char> tokenWriter,
        string payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        EncodeJws(tokenWriter, payload.AsSpan(), secretKey, signatureAlgorithmCode, extraHeaders, options);
    }

    /// <inheritdoc />
    public string EncodeJws(
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        EncodeJws(tokenBuffer, payload, secretKey, signatureAlgorithmCode, extraHeaders, options);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void EncodeJws(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        var byteCount = Encoding.UTF8.GetByteCount(payload);
        using var payloadLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = Encoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        EncodeJws(tokenWriter, payloadBytes, secretKey, signatureAlgorithmCode, extraHeaders, options);
    }

    /// <inheritdoc />
    public string EncodeJws(
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        EncodeJws(tokenBuffer, payload, secretKey, signatureAlgorithmCode, extraHeaders, options);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public void EncodeJws(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        SecretKey secretKey,
        string signatureAlgorithmCode,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null,
        JwsOptions? options = null)
    {
        var nonNullOptions = options ?? DefaultJwsOptions;

        var signatureAlgorithm = GetSignatureAlgorithm(signatureAlgorithmCode);

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
        EncodeJwsSignature(
            secretKey,
            signatureAlgorithm,
            encodedHeaderPart,
            encodedPayloadPart,
            tokenWriter);
    }

    private void EncodeJwsHeader(
        string algorithmCode,
        string keyId,
        IBufferWriter<char> tokenWriter,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders,
        JwsOptions options,
        out ReadOnlySpan<char> encodedHeaderPart)
    {
        var header = extraHeaders != null ?
            new Dictionary<string, object>(extraHeaders) :
            new Dictionary<string, object>();

        header.TryAdd("typ", "JWT");

        if (!string.IsNullOrEmpty(algorithmCode))
            header["alg"] = algorithmCode;

        if (!string.IsNullOrEmpty(keyId) && JoseOptions.AddKeyIdHeaderDuringEncode)
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

        using var headerLease = Serialize(header, out var headerBytes);
        encodedHeaderPart = WriteCompactSegment(headerBytes, tokenWriter);
    }

    private static IDisposable EncodeJwsPayload(
        ReadOnlySpan<byte> payload,
        IBufferWriter<char> tokenWriter,
        JwsOptions options,
        out ReadOnlySpan<char> encodedPayloadPart)
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
            var encodeResult = TryEncodeJose(options.EncodePayload, payload, encodedPayload, out var payloadCharsWritten);
            Debug.Assert(encodeResult && payloadCharsWritten == payloadCharCount);

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
