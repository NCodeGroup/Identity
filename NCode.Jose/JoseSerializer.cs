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
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.Jose.Exceptions;
using NCode.Jose.Internal;

namespace NCode.Jose;

/// <summary>
/// Provides the ability to encode and decode JWT values using JSON Object Signing and Encryption (JOSE).
/// </summary>
public interface IJoseSerializer
{
    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for signature validation.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string value, ISecretKeyCollection secretKeys);

    /// <summary>
    /// Attempts to validate a Json Web Token (JWT) and return the decoded payload.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for signature validation.</param>
    /// <param name="payload">An <see cref="string"/> to receive the decoded payload if validation was successful.</param>
    /// <returns><c>true</c> if the Json Web Token (JWT) was successfully decoded and validated; otherwise, <c>false</c>.</returns>
    bool TryDecode(string value, ISecretKeyCollection secretKeys, [MaybeNullWhen(false)] out string payload);

    /// <summary>
    /// Attempts to validate a Json Web Token (JWT) and return the decoded payload.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for signature validation.</param>
    /// <param name="payload">A  <see cref="string"/> that is to receive the decoded payload if validation was successful.</param>
    /// <param name="headers">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded headers if validation was successful.</param>
    /// <returns><c>true</c> if the Json Web Token (JWT) was successfully decoded and validated; otherwise, <c>false</c>.</returns>
    bool TryDecode(
        string value,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers);
}

/// <summary>
/// Provides a default implementation for the <see cref="IJoseSerializer"/> interface.
/// </summary>
public partial class JoseSerializer : IJoseSerializer
{
    private const int JwsSegmentCount = 3;
    private const int JweSegmentCount = 5;

    private static JsonSerializerOptions DeserializeOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new DictionaryJsonConverter()
        }
    };

    private static bool IsJson(string value) =>
        value.StartsWith('{') && value.EndsWith('}');

    private static ReadOnlySequenceSegment<char> Split(string value, out int count) =>
        StringSplitSequenceSegment.Split(value, '.', out count);

    private static IMemoryOwner<byte> DecodeBase64Url(string name, ReadOnlySpan<char> chars, bool isSensitive, out Span<byte> bytes)
    {
        var byteCount = Base64Url.GetByteCountForDecode(chars.Length);

        var lease = CryptoPool.Rent(byteCount, isSensitive);
        try
        {
            bytes = lease.Memory.Span;
            var decodeResult = Base64Url.TryDecode(chars, bytes, out var decodeBytesWritten);
            if (!decodeResult || decodeBytesWritten != byteCount)
            {
                throw new JoseException($"Failed to decode {name}.");
            }
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }

    private static T DeserializeBase64Url<T>(string name, ReadOnlySpan<char> chars, bool isSensitive = false)
    {
        using var lease = DecodeBase64Url(name, chars, isSensitive, out var bytes);
        return Deserialize<T>(name, bytes);
    }

    private static T Deserialize<T>(string name, ReadOnlySpan<byte> bytes) =>
        JsonSerializer.Deserialize<T>(bytes, DeserializeOptions) ??
        throw new JoseException($"Failed to deserialize {name}");

    private static bool TryGetHeader<T>(IReadOnlyDictionary<string, object> collection, string key, [MaybeNullWhen(false)] out T value)
    {
        if (collection.TryGetValue(key, out var obj) && obj is T converted)
        {
            value = converted;
            return true;
        }

        value = default;
        return false;
    }

    private static byte[] DecodePayload(bool b64, ReadOnlySpan<char> encodedPayload)
    {
        var charCount = encodedPayload.Length;
        var byteCount = b64 ? Base64Url.GetByteCountForDecode(charCount) : Encoding.UTF8.GetByteCount(encodedPayload);
        var bytes = new byte[byteCount];

        if (b64)
        {
            var result = Base64Url.TryDecode(encodedPayload, bytes, out var bytesWritten);
            Debug.Assert(result && bytesWritten == byteCount);
        }
        else
        {
            var bytesWritten = Encoding.UTF8.GetBytes(encodedPayload, bytes);
            Debug.Assert(bytesWritten == byteCount);
        }

        return bytes;
    }

    private IAlgorithmProvider AlgorithmProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSerializer"/> class.
    /// </summary>
    /// <param name="algorithmProvider">An <see cref="IAlgorithmProvider"/> that is used to lookup signing keys for signature validation.</param>
    public JoseSerializer(IAlgorithmProvider algorithmProvider)
    {
        AlgorithmProvider = algorithmProvider;
    }

    /// <inheritdoc />
    public string Decode(string value, ISecretKeyCollection secretKeys) =>
        !TryDecode(value, secretKeys, out var payload, out _) ?
            throw new IntegrityJoseException("Signature validation failed.") :
            payload;

    /// <inheritdoc />
    public bool TryDecode(
        string value,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload) =>
        TryDecode(value, secretKeys, out payload, out _);

    /// <inheritdoc />
    public bool TryDecode(
        string value,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers)
    {
        var segment = Split(value, out var count);
        return count switch
        {
            JwsSegmentCount => TryDecodeJws(segment, secretKeys, out payload, out headers),
            JweSegmentCount => TryDecodeJwe(segment, secretKeys, out payload, out headers),
            _ => throw new InvalidOperationException()
        };
    }

    private bool TryDecodeJws(
        ReadOnlySequenceSegment<char> segment,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers)
    {
        // TODO: should we use CryptoPool?

        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */

        // JWS Protected Header
        var encodedHeader = segment.Memory.Span;
        var headerBytes = Base64Url.Decode(encodedHeader);
        var deserializedHeader = Deserialize<IReadOnlyDictionary<string, object>>("JWS Protected Header", headerBytes);

        if (!TryGetHeader<string>(deserializedHeader, "alg", out var algorithmCode))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
        }

        if (!TryGetHeader<bool>(deserializedHeader, "b64", out var b64))
        {
            b64 = true;
        }

        // JWS Payload
        segment = segment.Next ?? throw new InvalidOperationException();
        var encodedPayload = segment.Memory.Span;
        var payloadBytes = DecodePayload(b64, encodedPayload);

        // JWS Signature
        segment = segment.Next ?? throw new InvalidOperationException();
        var encodedSignature = segment.Memory.Span;
        var expectedSignature = Base64Url.Decode(encodedSignature);

        Debug.Assert(segment.Next is null);

        if (algorithmCode == AlgorithmCodes.DigitalSignature.None)
        {
            var isValid = expectedSignature.Length == 0;
            payload = isValid ? Encoding.UTF8.GetString(payloadBytes) : null;
            headers = isValid ? deserializedHeader : null;
            return isValid;
        }

        if (!AlgorithmProvider.TryGetSignatureAlgorithm(algorithmCode, out var algorithm))
        {
            throw new InvalidAlgorithmJoseException($"No registered signing algorithm for `{algorithmCode}` was found.");
        }

        var signatureInput = GetSignatureInput(encodedHeader, encodedPayload);

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

        if (TryGetHeader<string>(deserializedHeader, "kid", out var keyId) &&
            secretKeys.TryGetByKeyId(keyId, out var specificKey) &&
            TryVerify(specificKey, out payload))
        {
            headers = deserializedHeader;
            return true;
        }

        foreach (var secretKey in secretKeys)
        {
            if (!TryVerify(secretKey, out payload)) continue;
            headers = deserializedHeader;
            return true;
        }

        // no matching signing key
        payload = null;
        headers = deserializedHeader;
        return false;
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

    private bool TryDecodeJwe(
        ReadOnlySequenceSegment<char> segment,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers)
    {
        // Debug.Assert(jwt.Length > 0);
        // Debug.Assert(indices.Count == JweSegmentCount);

        /*

        // JOSE Header
        // JWE Encrypted Key
        // JWE Initialization Vector
        // JWE AAD
        // JWE Ciphertext
        // JWE Authentication Tag

        == Compact encoding ==
        BASE64URL(UTF8(JWE Protected Header)) || '.' ||
        BASE64URL(JWE Encrypted Key) || '.' ||
        BASE64URL(JWE Initialization Vector) || '.' ||
        BASE64URL(JWE Ciphertext) || '.' ||
        BASE64URL(JWE Authentication Tag)

        // JSON encoding:
        // JWE Protected Header
        // JWE Shared Unprotected Header
        // JWE Per-Recipient Unprotected Header

        */

        // protectedHeaderBytes
        // encryptedCek
        // iv
        // ciphertext
        // authTag

        // var encodedHeader = segments[0];
        // var headerBytes = Base64Url.Decode(encodedHeader);
        // var deserializedHeaders = DeserializeHeaders(headerBytes);

        throw new NotImplementedException();
    }
}
