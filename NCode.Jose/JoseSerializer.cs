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
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using Nerdbank.Streams;

namespace NCode.Jose;

/// <summary>
/// Provides the ability to encode and decode JWT values using JSON Object Signing and Encryption (JOSE).
/// </summary>
public interface IJoseSerializer
{
    // Encode, Serialize, etc...

    // TODO

    // Parse, Decode, Deserialize, etc...

    /// <summary>
    /// Parses a Json Web Token (JWT) and returns the parsed JWT in compact form.
    /// This method supports both JWS and JWE (i.e. encrypted) tokens in compact form.
    /// The main purpose of this method is to extract the JWT header before validation/decryption.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to parse.</param>
    /// <returns>The parsed JWT in compact form.</returns>
    CompactToken ParseCompact(string value);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation/decryption.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string value, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload and header.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation/decryption.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string value, SecretKey secretKey, out IReadOnlyDictionary<string, object> header);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="compactToken">The parsed JWT in compact form to decode and validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation/decryption.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(CompactToken compactToken, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(string value, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload and header.
    /// </summary>
    /// <param name="value">The Json Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation/decryption.</param>
    /// <param name="header">An <see cref="IReadOnlyDictionary{TKey,TValue}"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(string value, SecretKey secretKey, out IReadOnlyDictionary<string, object> header);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="compactToken">The parsed JWT in compact form to deserialize and validate.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(CompactToken compactToken, SecretKey secretKey);
}

/// <summary>
/// Provides a default implementation for the <see cref="IJoseSerializer"/> interface.
/// </summary>
public partial class JoseSerializer : IJoseSerializer
{
    private const int JwsSegmentCount = 3;
    private const int JweSegmentCount = 5;

    internal static IMemoryOwner<byte> DecodeBase64Url(string name, ReadOnlySpan<char> chars, bool isSensitive, out Span<byte> bytes)
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

    private JoseOptions JoseOptions { get; }

    private IAlgorithmProvider AlgorithmProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSerializer"/> class.
    /// </summary>
    /// <param name="optionsAccessor">An accessor that provides <see cref="JoseOptions"/>.</param>
    /// <param name="algorithmProvider">An <see cref="IAlgorithmProvider"/> that is used to lookup signing keys for signature validation.</param>
    public JoseSerializer(IOptions<JoseOptions> optionsAccessor, IAlgorithmProvider algorithmProvider)
    {
        JoseOptions = optionsAccessor.Value;
        AlgorithmProvider = algorithmProvider;
    }

    /// <inheritdoc />
    public CompactToken ParseCompact(string value)
    {
        var segments = value.SplitSegments('.');
        var tokenType = segments.Count switch
        {
            JwsSegmentCount => JoseConstants.JWS,
            JweSegmentCount => JoseConstants.JWE,
            _ => throw new ArgumentException("The specified value does not represent a valid JOSE token in compact form.", nameof(value))
        };
        return new CompactToken(tokenType, segments, JoseOptions);
    }

    /// <inheritdoc />
    public string Decode(string value, SecretKey secretKey) =>
        Decode(ParseCompact(value), secretKey);

    /// <inheritdoc />
    public string Decode(string value, SecretKey secretKey, out IReadOnlyDictionary<string, object> header)
    {
        var compactToken = ParseCompact(value);
        var payload = Decode(compactToken, secretKey);
        header = compactToken.DeserializedHeader;
        return payload;
    }

    /// <inheritdoc />
    public string Decode(CompactToken compactToken, SecretKey secretKey) =>
        compactToken.ProtectionType switch
        {
            JoseConstants.JWS => DecodeJws(compactToken, secretKey),
            JoseConstants.JWE => DecodeJwe(compactToken, secretKey),
            _ => throw new InvalidOperationException()
        };

    private static string DecodeUtf8(ReadOnlySequence<byte> byteSequence)
    {
        using var charSequence = new Sequence<char>(ArrayPool<char>.Shared);
        Encoding.UTF8.GetChars(byteSequence, charSequence);
        return charSequence.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string value, SecretKey secretKey) =>
        Deserialize<T>(ParseCompact(value), secretKey);

    /// <inheritdoc />
    public T? Deserialize<T>(string value, SecretKey secretKey, out IReadOnlyDictionary<string, object> header)
    {
        var compactToken = ParseCompact(value);
        var payload = Deserialize<T>(compactToken, secretKey);
        header = compactToken.DeserializedHeader;
        return payload;
    }

    /// <inheritdoc />
    public T? Deserialize<T>(CompactToken compactToken, SecretKey secretKey) =>
        compactToken.ProtectionType switch
        {
            JoseConstants.JWS => DeserializeJws<T>(compactToken, secretKey),
            JoseConstants.JWE => DeserializeJwe<T>(compactToken, secretKey),
            _ => throw new InvalidOperationException()
        };

    private T? Deserialize<T>(ReadOnlySequence<byte> byteSequence)
    {
        var jsonOptions = JoseOptions.JsonSerializerOptions;
        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = jsonOptions.AllowTrailingCommas,
            CommentHandling = jsonOptions.ReadCommentHandling,
            MaxDepth = jsonOptions.MaxDepth
        };
        var reader = new Utf8JsonReader(byteSequence, readerOptions);
        return JsonSerializer.Deserialize<T>(ref reader, jsonOptions);
    }
}
