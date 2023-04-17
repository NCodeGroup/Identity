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
using System.Text.Json;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Serialization;

namespace NIdentity.OpenId.Jose;

/// <summary>
/// Provides the ability to encode and decode JWT values using JSON Object Signing and Encryption (JOSE).
/// </summary>
public interface IJoseSerializer
{
    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="jwt">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for signature validation.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string jwt, ISecretKeyCollection secretKeys);

    /// <summary>
    /// Attempts to validate a Json Web Token (JWT) and return the decoded payload.
    /// </summary>
    /// <param name="jwt">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for signature validation.</param>
    /// <param name="payload">An <see cref="string"/> to receive the decoded payload if validation was successful.</param>
    /// <returns><c>true</c> if the Json Web Token (JWT) was successfully decoded and validated; otherwise, <c>false</c>.</returns>
    bool TryDecode(string jwt, ISecretKeyCollection secretKeys, [MaybeNullWhen(false)] out string payload);

    /// <summary>
    /// Attempts to validate a Json Web Token (JWT) and return the decoded payload.
    /// </summary>
    /// <param name="jwt">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for signature validation.</param>
    /// <param name="payload">A  <see cref="string"/> that is to receive the decoded payload if validation was successful.</param>
    /// <param name="headers">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded headers if validation was successful.</param>
    /// <returns><c>true</c> if the Json Web Token (JWT) was successfully decoded and validated; otherwise, <c>false</c>.</returns>
    bool TryDecode(
        string jwt,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers);
}

/// <summary>
/// Provides a default implementation for the <see cref="IJoseSerializer"/> interface.
/// </summary>
public class JoseSerializer : IJoseSerializer
{
    private const int JwsSegmentCount = 3;
    private const int JweSegmentCount = 5;
    private const int MaxJwtSegmentCount = 5;

    private static JsonSerializerOptions DeserializeOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new DictionaryJsonConverter()
        }
    };

    private IAlgorithmCollection AlgorithmCollection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSerializer"/> class.
    /// </summary>
    /// <param name="algorithmCollection">An <see cref="IAlgorithmCollection"/> that is used to lookup signing keys for signature validation.</param>
    public JoseSerializer(IAlgorithmCollection algorithmCollection)
    {
        AlgorithmCollection = algorithmCollection;
    }

    /// <inheritdoc />
    public string Decode(string jwt, ISecretKeyCollection secretKeys) =>
        !TryDecode(jwt, secretKeys, out var payload, out _) ?
            throw new IntegrityException("Signature validation failed.") :
            payload;

    /// <inheritdoc />
    public bool TryDecode(
        string jwt,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload) =>
        TryDecode(jwt, secretKeys, out payload, out _);

    /// <inheritdoc />
    public bool TryDecode(
        string jwt,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers)
    {
        var segments = jwt.Split('.', MaxJwtSegmentCount + 1);
        return segments.Length switch
        {
            JwsSegmentCount => TryDecode(segments, secretKeys, out payload, out headers),
            JweSegmentCount => TryDecrypt(segments, secretKeys, out payload, out headers),
            _ => throw new JoseException("The given value is not a valid JWT. Too many segments.")
        };
    }

    internal virtual IReadOnlyDictionary<string, object> DeserializeHeaders(byte[] bytes) =>
        JsonSerializer.Deserialize<IReadOnlyDictionary<string, object>>(bytes, DeserializeOptions) ??
        throw new JoseException("Unable to deserialize the JWT headers.");

    private bool TryDecode(
        IReadOnlyList<string> segments,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers)
    {
        Debug.Assert(segments.Count == JwsSegmentCount);

        var encodedHeader = segments[0];
        var headerBytes = Base64Url.Decode(encodedHeader);
        var deserializedHeaders = DeserializeHeaders(headerBytes);

        if (!TryGetValue<string>(deserializedHeaders, "alg", out var algorithmCode))
        {
            throw new JoseException("The 'alg' header is missing.");
        }

        if (!TryGetValue<bool>(deserializedHeaders, "b64", out var b64))
        {
            b64 = true;
        }

        var encodedPayload = segments[1];
        var payloadBytes = b64 ? Base64Url.Decode(encodedPayload) : Encoding.UTF8.GetBytes(encodedPayload);

        var encodedSignature = segments[2];
        var expectedSignature = Base64Url.Decode(encodedSignature);

        if (algorithmCode == AlgorithmCodes.DigitalSignature.None)
        {
            var isValid = expectedSignature.Length == 0;
            payload = isValid ? Encoding.UTF8.GetString(payloadBytes) : null;
            headers = isValid ? deserializedHeaders : null;
            return isValid;
        }

        if (!AlgorithmCollection.TryGetSignatureAlgorithm(algorithmCode, out var algorithm))
        {
            throw new InvalidAlgorithmException($"No registered signing algorithm for `{algorithmCode}` was found.");
        }

        var signatureInput = GetSignatureInput(encodedHeader, encodedPayload);

        bool TryVerify(SecretKey secretKey, [MaybeNullWhen(false)] out string payload)
        {
            using var provider = secretKey.CreateSignatureProvider(algorithm);

            if (provider.Verify(signatureInput, expectedSignature))
            {
                payload = Encoding.UTF8.GetString(payloadBytes);
                return true;
            }

            payload = null;
            return false;
        }

        if (TryGetValue<string>(deserializedHeaders, "kid", out var keyId) &&
            secretKeys.TryGet(keyId, out var specificKey) &&
            TryVerify(specificKey, out payload))
        {
            headers = deserializedHeaders;
            return true;
        }

        foreach (var secretKey in secretKeys)
        {
            if (!TryVerify(secretKey, out payload)) continue;
            headers = deserializedHeaders;
            return true;
        }

        // no matching signing key
        payload = null;
        headers = deserializedHeaders;
        return false;
    }

    private static byte[] GetSignatureInput(string encodedHeader, string encodedPayload)
    {
        var headerByteCount = Encoding.UTF8.GetByteCount(encodedHeader);
        var payloadByteCount = Encoding.UTF8.GetByteCount(encodedPayload);

        var result = new byte[headerByteCount + 1 + payloadByteCount];

        Encoding.UTF8.GetBytes(encodedHeader, result);
        result[headerByteCount] = (byte)'.';
        Encoding.UTF8.GetBytes(encodedPayload, result.AsSpan(headerByteCount + 1));

        return result;
    }

    private bool TryDecrypt(
        IReadOnlyList<string> segments,
        ISecretKeyCollection secretKeys,
        [MaybeNullWhen(false)] out string payload,
        [MaybeNullWhen(false)] out IReadOnlyDictionary<string, object> headers)
    {
        Debug.Assert(segments.Count == JweSegmentCount);

        throw new NotImplementedException();
    }

    private static bool TryGetValue<T>(IReadOnlyDictionary<string, object> collection, string key, [MaybeNullWhen(false)] out T value)
    {
        if (collection.TryGetValue(key, out var obj) && obj is T converted)
        {
            value = converted;
            return true;
        }

        value = default;
        return false;
    }
}
