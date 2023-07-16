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
using System.Text;
using System.Text.Json;
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.Json;
using Nerdbank.Streams;

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
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for cryptographic operations.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(
        string value,
        ISecretKeyCollection secretKeys);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload and header.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for cryptographic operations.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(
        string value,
        ISecretKeyCollection secretKeys,
        out IReadOnlyDictionary<string, object> header);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for cryptographic operations.</param>
    /// <param name="options">The options to control JSON deserialization behavior.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(
        string value,
        ISecretKeyCollection secretKeys,
        JsonSerializerOptions options);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload and header.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKeys">An <see cref="ISecretKeyCollection"/> with <see cref="SecretKey"/> instances used for cryptographic operations.</param>
    /// <param name="options">The options to control JSON deserialization behavior.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(
        string value,
        ISecretKeyCollection secretKeys,
        JsonSerializerOptions options,
        out IReadOnlyDictionary<string, object> header);
}

/// <summary>
/// Provides a default implementation for the <see cref="IJoseSerializer"/> interface.
/// </summary>
public partial class JoseSerializer : IJoseSerializer
{
    private const int JwsSegmentCount = 3;
    private const int JweSegmentCount = 5;

    private static JsonSerializerOptions DeserializeHeaderOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new DictionaryJsonConverter()
        }
    };

    private static IMemoryOwner<byte> DecodeBase64Url(string name, ReadOnlySpan<char> chars, bool isSensitive, out Span<byte> bytes)
    {
        var byteCount = Base64Url.GetByteCountForDecode(chars.Length);
        var lease = CryptoPool.Rent(byteCount, isSensitive, out bytes);
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

    private static Dictionary<string, object> DeserializeHeader(string name, ReadOnlySpan<char> encodedHeader)
    {
        using var lease = DecodeBase64Url(name, encodedHeader, isSensitive: false, out var utf8Json);
        var header = JsonSerializer.Deserialize<Dictionary<string, object>>(utf8Json, DeserializeHeaderOptions);
        return header ?? throw new JoseException($"Failed to deserialize {name}");
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

    private static string DecodeUtf8(ReadOnlySequence<byte> byteSequence)
    {
        using var charSequence = new Sequence<char>(ArrayPool<char>.Shared);
        Encoding.UTF8.GetChars(byteSequence, charSequence);
        return charSequence.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public T? Deserialize<T>(
        string value,
        ISecretKeyCollection secretKeys,
        JsonSerializerOptions options) =>
        Deserialize<T>(value, secretKeys, options, out _);

    /// <inheritdoc />
    public T? Deserialize<T>(
        string value,
        ISecretKeyCollection secretKeys,
        JsonSerializerOptions options,
        out IReadOnlyDictionary<string, object> header)
    {
        var segments = StringSegments.Split(value, '.');
        return segments.Count switch
        {
            JwsSegmentCount => DeserializeJws<T>(segments, secretKeys, options, out header),
            JweSegmentCount => DeserializeJweCompact<T>(segments, secretKeys, options, out header),
            _ => throw new InvalidOperationException()
        };
    }

    private static T? Deserialize<T>(
        ReadOnlySequence<byte> byteSequence,
        JsonSerializerOptions options)
    {
        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.ReadCommentHandling,
            MaxDepth = options.MaxDepth
        };
        var reader = new Utf8JsonReader(byteSequence, readerOptions);
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }
}
