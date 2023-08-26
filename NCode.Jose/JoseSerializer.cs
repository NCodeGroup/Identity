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
public partial interface IJoseSerializer
{
    /// <summary>
    /// Parses a Json Web Token (JWT) and returns the parsed JWT in compact form.
    /// This method supports both JWS and JWE (i.e. encrypted) tokens in compact form.
    /// The main purpose of this method is to extract the JWT header before validation/decryption.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to parse.</param>
    /// <returns>The parsed JWT in compact form.</returns>
    CompactJwt ParseCompactJwt(string token);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string token, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload and header.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(string token, SecretKey secretKey, out JsonElement header);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the decoded payload.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to decode and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <returns>The decoded payload from Json Web Token (JWT).</returns>
    string Decode(CompactJwt compactJwt, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(string token, SecretKey secretKey);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload and header.
    /// </summary>
    /// <param name="token">The Json Web Token (JWT) to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <param name="header">A <see cref="JsonElement"/> that is to receive the decoded JOSE header if validation was successful.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(string token, SecretKey secretKey, out JsonElement header);

    /// <summary>
    /// Validates a Json Web Token (JWT) and returns the deserialized payload.
    /// </summary>
    /// <param name="compactJwt">The parsed JWT in compact form to deserialize and validate.</param>
    /// <param name="secretKey">The Key Encryption Key (KEK) to use for validation/decryption.</param>
    /// <typeparam name="T">The type of the payload to deserialize.</typeparam>
    /// <returns>The deserialized payload from Json Web Token (JWT).</returns>
    T? Deserialize<T>(CompactJwt compactJwt, SecretKey secretKey);
}

/// <summary>
/// Provides a default implementation for the <see cref="IJoseSerializer"/> interface.
/// </summary>
public partial class JoseSerializer : IJoseSerializer
{
    private const int JwsSegmentCount = 3;
    private const int JweSegmentCount = 5;

    internal static IDisposable DecodeBase64Url(string name, ReadOnlySpan<char> chars, bool isSensitive, out Span<byte> bytes)
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
    public CompactJwt ParseCompactJwt(string token)
    {
        var segments = token.SplitSegments('.');
        var protectionType = segments.Count switch
        {
            JwsSegmentCount => JoseConstants.JWS,
            JweSegmentCount => JoseConstants.JWE,
            _ => throw new ArgumentException("The specified value does not represent a valid JOSE token in compact form.", nameof(token))
        };
        return new CompactJwt(protectionType, segments, JoseOptions.JsonSerializerOptions);
    }

    /// <inheritdoc />
    public string Decode(string token, SecretKey secretKey) =>
        Decode(ParseCompactJwt(token), secretKey);

    /// <inheritdoc />
    public string Decode(string token, SecretKey secretKey, out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        var payload = Decode(compact, secretKey);
        header = compact.DeserializedHeader;
        return payload;
    }

    /// <inheritdoc />
    public string Decode(CompactJwt compactJwt, SecretKey secretKey) =>
        compactJwt.ProtectionType switch
        {
            JoseConstants.JWS => DecodeJws(compactJwt, secretKey),
            JoseConstants.JWE => DecodeJwe(compactJwt, secretKey),
            _ => throw new InvalidOperationException()
        };

    private static string DecodeUtf8(ReadOnlySequence<byte> byteSequence)
    {
        using var charBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        Encoding.UTF8.GetChars(byteSequence, charBuffer);
        return charBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string token, SecretKey secretKey) =>
        Deserialize<T>(ParseCompactJwt(token), secretKey);

    /// <inheritdoc />
    public T? Deserialize<T>(string token, SecretKey secretKey, out JsonElement header)
    {
        var compact = ParseCompactJwt(token);
        var payload = Deserialize<T>(compact, secretKey);
        header = compact.DeserializedHeader;
        return payload;
    }

    /// <inheritdoc />
    public T? Deserialize<T>(CompactJwt compactJwt, SecretKey secretKey) =>
        compactJwt.ProtectionType switch
        {
            JoseConstants.JWS => DeserializeJws<T>(compactJwt, secretKey),
            JoseConstants.JWE => DeserializeJwe<T>(compactJwt, secretKey),
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

    private IDisposable Serialize<T>(T value, out ReadOnlySpan<byte> bytes)
    {
        var buffer = new Sequence<byte>(ArrayPool<byte>.Shared)
        {
            // increase our chances of getting a single-segment buffer
            MinimumSpanLength = 1024
        };
        try
        {
            using var writer = new Utf8JsonWriter(buffer);
            JsonSerializer.Serialize(writer, value, JoseOptions.JsonSerializerOptions);

            var sequence = buffer.AsReadOnlySequence;
            if (sequence.IsSingleSegment)
            {
                bytes = sequence.FirstSpan;
                return buffer;
            }

            var byteCount = (int)sequence.Length;
            var lease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> span);
            try
            {
                sequence.CopyTo(span);
                buffer.Dispose();
                bytes = span;
            }
            catch
            {
                lease.Dispose();
                throw;
            }

            return lease;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private IDisposable EncodeJose<T>(bool b64, T value, out ReadOnlySpan<char> chars)
    {
        using var bytesLease = Serialize(value, out var bytes);
        return EncodeJose(b64, bytes, out chars);
    }

    private static bool TryEncodeJose(bool b64, ReadOnlySpan<byte> bytes, Span<char> chars, out int charsWritten)
    {
        if (b64)
        {
            return Base64Url.TryEncode(bytes, chars, out charsWritten);
        }

        charsWritten = Encoding.UTF8.GetChars(bytes, chars);
        return charsWritten > 0;
    }

    private static IDisposable EncodeJose(bool b64, ReadOnlySpan<byte> bytes, out ReadOnlySpan<char> chars)
    {
        var charCount = b64 ?
            Base64Url.GetCharCountForEncode(bytes.Length) :
            Encoding.UTF8.GetCharCount(bytes);

        var charLease = MemoryPool<char>.Shared.Rent(charCount);
        try
        {
            var span = charLease.Memory.Span[..charCount];
            var encodeResult = TryEncodeJose(b64, bytes, span, out var charsWritten);
            Debug.Assert(encodeResult && charsWritten == charCount);

            chars = span;
        }
        catch
        {
            charLease.Dispose();
            throw;
        }

        return charLease;
    }

    private static IDisposable Encode(Encoding encoding, ReadOnlySpan<char> chars, out ReadOnlySpan<byte> bytes)
    {
        var byteCount = encoding.GetByteCount(chars);
        var byteLease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> span);
        try
        {
            var bytesWritten = encoding.GetBytes(chars, span);
            Debug.Assert(bytesWritten == byteCount);

            bytes = span;
        }
        catch
        {
            byteLease.Dispose();
            throw;
        }

        return byteLease;
    }

    private static void WriteCompactSegment(ReadOnlySpan<char> chars, IBufferWriter<char> writer, bool addDot = true)
    {
        var charCount = chars.Length;
        var dotLength = addDot ? 1 : 0;

        var span = writer.GetSpan(charCount + dotLength);
        chars.CopyTo(span);

        if (addDot)
        {
            span[charCount] = '.';
        }

        writer.Advance(charCount + dotLength);
    }

    private static ReadOnlySpan<char> WriteCompactSegment(ReadOnlySpan<byte> bytes, IBufferWriter<char> writer, bool addDot = true, bool b64 = true)
    {
        var charCount = b64 ?
            Base64Url.GetCharCountForEncode(bytes.Length) :
            Encoding.UTF8.GetCharCount(bytes);

        var dotLength = addDot ? 1 : 0;
        var totalLength = charCount + dotLength;

        var span = writer.GetSpan(totalLength);
        var encodeResult = TryEncodeJose(b64, bytes, span, out var charsWritten);
        Debug.Assert(encodeResult && charsWritten == charCount);

        if (addDot)
        {
            span[charsWritten] = '.';
        }

        writer.Advance(totalLength);

        return span[..totalLength];
    }
}
