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
using NIdentity.OpenId.Serialization;

namespace NIdentity.OpenId.Logic;

internal interface IJwtDecoder2
{
    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded JSON payload.
    /// </summary>
    /// <param name="jwt">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> used for signature validation.</param>
    /// <returns>The decoded payload from Json Web Token (JWT) in JSON format.</returns>
    string Decode(string jwt, ISecretKeyCollection secretKeys);
}

internal class JwtDecoder : IJwtDecoder2
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

    public JwtDecoder(IAlgorithmCollection algorithmCollection)
    {
        AlgorithmCollection = algorithmCollection;
    }

    public string Decode(string jwt, ISecretKeyCollection secretKeys)
    {
        var segments = jwt.Split('.', MaxJwtSegmentCount + 1);
        return segments.Length switch
        {
            JwsSegmentCount => Decode(segments, secretKeys),
            JweSegmentCount => Decrypt(segments, secretKeys),
            _ => throw new ArgumentException("Too many segments", nameof(jwt))
        };
    }

    internal virtual IReadOnlyDictionary<string, object> DeserializeHeaders(byte[] bytes) =>
        JsonSerializer.Deserialize<IReadOnlyDictionary<string, object>>(bytes, DeserializeOptions) ?? throw new InvalidOperationException();

    private string Decode(IReadOnlyList<string> segments, ISecretKeyCollection secretKeys)
    {
        Debug.Assert(segments.Count == JwsSegmentCount);

        var encodedHeader = segments[0];
        var headerBytes = Base64Url.Decode(encodedHeader);
        var headers = DeserializeHeaders(headerBytes);

        if (!TryGetValue<string>(headers, "alg", out var algorithmCode))
        {
            throw new InvalidOperationException();
        }

        if (!TryGetValue<bool>(headers, "b64", out var b64))
        {
            b64 = true;
        }

        var encodedPayload = segments[1];
        var payloadBytes = b64 ? Base64Url.Decode(encodedPayload) : Encoding.UTF8.GetBytes(encodedPayload);

        if (algorithmCode == AlgorithmCodes.DigitalSignature.None)
        {
            return Encoding.UTF8.GetString(payloadBytes);
        }

        if (!AlgorithmCollection.TryGetSignatureAlgorithm(algorithmCode, out var algorithm))
        {
            throw new InvalidOperationException();
        }

        var encodedSignature = segments[2];
        var expectedSignature = Base64Url.Decode(encodedSignature);
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

        var payload = string.Empty;

        if (TryGetValue<string>(headers, "kid", out var keyId) &&
            secretKeys.TryGet(keyId, out var specificKey) &&
            TryVerify(specificKey, out payload))
        {
            return payload;
        }

        if (secretKeys.Any(secretKey => TryVerify(secretKey, out payload)))
        {
            return payload!;
        }

        // no matching signing key
        throw new InvalidOperationException();
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

    private string Decrypt(string[] segments, ISecretKeyCollection secretKeys)
    {
        throw new NotImplementedException();
    }

    private static bool TryGetValue<T>(IReadOnlyDictionary<string, object> collection, string key, [MaybeNullWhen(false)] out T value)
    {
        if (!collection.TryGetValue(key, out var obj) || obj is not T converted)
        {
            value = default;
            return false;
        }

        value = converted;
        return true;
    }
}
