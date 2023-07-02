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
    string Decode(
        string value,
        ISecretKeyCollection secretKeys);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload and header.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for signature validation.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(
        string value,
        ISecretKeyCollection secretKeys,
        out IReadOnlyDictionary<string, object> header);
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

    private static IMemoryOwner<byte> RentBuffer(int minBufferSize, bool isSensitive, out Span<byte> bytes)
    {
        var lease = isSensitive ? CryptoPool.Rent(minBufferSize) : MemoryPool<byte>.Shared.Rent(minBufferSize);
        bytes = lease.Memory.Span[..minBufferSize];
        return lease;
    }

    private static IMemoryOwner<byte> DecodeBase64Url(string name, ReadOnlySpan<char> chars, bool isSensitive, out Span<byte> bytes)
    {
        var byteCount = Base64Url.GetByteCountForDecode(chars.Length);
        var lease = RentBuffer(byteCount, isSensitive, out bytes);
        try
        {
            var decodeResult = Base64Url.TryDecode(chars, bytes, out var decodeBytesWritten);
            if (!decodeResult || decodeBytesWritten != byteCount)
            {
                throw new JoseException($"Failed to base64url decode {name}.");
            }
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }

    private static T DeserializeUtf8JsonAfterBase64Url<T>(string name, ReadOnlySpan<char> chars)
    {
        using var lease = DecodeBase64Url(name, chars, isSensitive: false, out var utf8JsonBytes);
        return DeserializeUtf8Json<T>(name, utf8JsonBytes);
    }

    private static T DeserializeUtf8Json<T>(string name, ReadOnlySpan<byte> utf8JsonBytes) =>
        JsonSerializer.Deserialize<T>(utf8JsonBytes, DeserializeOptions) ??
        throw new JoseException($"Failed to deserialize {name}");

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
    public string Decode(
        string value,
        ISecretKeyCollection secretKeys) =>
        Decode(value, secretKeys, out _);

    /// <inheritdoc />
    public string Decode(
        string value,
        ISecretKeyCollection secretKeys,
        out IReadOnlyDictionary<string, object> header)
    {
        var segments = StringSegments.Split(value, '.');
        return segments.Count switch
        {
            JwsSegmentCount => DecodeJws(segments, secretKeys, out header),
            JweSegmentCount => DecodeJweCompact(segments, secretKeys, out header),
            _ => throw new InvalidOperationException()
        };
    }
}
