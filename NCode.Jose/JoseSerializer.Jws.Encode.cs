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
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.Extensions;
using NCode.Jose.Internal;
using NCode.Jose.SecretKeys;

namespace NCode.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Creates a new <see cref="JoseEncoder"/> with the specified signing credentials and options.
    /// </summary>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <returns>The newly created <see cref="JoseEncoder"/> instance.</returns>
    JoseEncoder CreateEncoder(
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions signingOptions);

    /// <summary>
    /// Encodes a JWS token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JWS token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="signingCredentials">The JOSE signing credentials.</param>
    /// <param name="signingOptions">The JOSE signing options.</param>
    /// <param name="extraHeaders">Any additional headers in include in the JOSE header.</param>
    void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null);
}

partial class JoseSerializer
{
    /// <inheritdoc />
    public JoseEncoder CreateEncoder(
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions signingOptions) =>
        new JoseSigningEncoder(this, signingCredentials, signingOptions);

    /// <inheritdoc />
    public void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<byte> payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var effectiveOptions = signingOptions ?? JoseSigningOptions.Default;

        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */

        var (secretKey, signatureAlgorithm) = signingCredentials;

        // BASE64URL(UTF8(JWS Protected Header)) || '.'
        EncodeJwsHeader(
            signatureAlgorithm.Code,
            secretKey.KeyId,
            tokenWriter,
            effectiveOptions,
            extraHeaders,
            out var encodedHeaderPart);

        // BASE64URL(JWS Payload) || '.'
        using var _ = EncodeJwsPayload(
            payload,
            tokenWriter,
            effectiveOptions,
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
        string? keyId,
        IBufferWriter<char> tokenWriter,
        JoseSigningOptions options,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders,
        out ReadOnlySpan<char> encodedHeaderPart)
    {
        var header = extraHeaders != null ?
            new Dictionary<string, object>(extraHeaders) :
            new Dictionary<string, object>();

        header.TryAdd(JoseClaimNames.Header.Typ, JoseConstants.Jwt);

        if (!string.IsNullOrEmpty(algorithmCode))
            header[JoseClaimNames.Header.Alg] = algorithmCode;

        if (!string.IsNullOrEmpty(keyId) && options.AddKeyIdHeader)
            header[JoseClaimNames.Header.Kid] = keyId;

        if (!options.EncodePayload)
        {
            var critical = new HashSet<string> { JoseClaimNames.Header.B64 };
            if (header.TryGetValue<IEnumerable<string>>(JoseClaimNames.Header.Crit, out var existing))
            {
                critical.UnionWith(existing);
            }

            header[JoseClaimNames.Header.B64] = false;
            header[JoseClaimNames.Header.Crit] = critical;
        }

        using var headerLease = SerializeJson(header, options: null, out var headerBytes);
        encodedHeaderPart = WriteCompactSegment(headerBytes, tokenWriter);
    }

    private static IDisposable EncodeJwsPayload(
        ReadOnlySpan<byte> payload,
        IBufferWriter<char> tokenWriter,
        JoseSigningOptions options,
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
