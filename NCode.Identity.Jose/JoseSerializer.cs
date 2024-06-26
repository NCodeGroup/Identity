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
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.CryptoMemory;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using Nerdbank.Streams;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides a default implementation for the <see cref="IJoseSerializer"/> interface.
/// </summary>
[PublicAPI]
public partial class JoseSerializer : IJoseSerializer
{
    private const int JwsSegmentCount = 3;
    private const int JweSegmentCount = 5;

    /// <summary>
    /// Provides the ability to decode binary data from Base64Url encoding using memory pooling.
    /// </summary>
    /// <param name="chars">The base64url data to decode.</param>
    /// <param name="isSensitive">Indicates whether the buffer should be pinned during it's lifetime and securely zeroed when returned.</param>
    /// <param name="bytes">When this method returns, contains the buffer with the decoded data.</param>
    /// <returns>An <see cref="IDisposable"/> that manages the lifetime of the lease.</returns>
    public static IDisposable DecodeBase64Url(ReadOnlySpan<char> chars, bool isSensitive, out Span<byte> bytes)
    {
        var byteCount = Base64Url.GetByteCountForDecode(chars.Length);
        var lease = CryptoPool.Rent(byteCount, isSensitive, out bytes);
        try
        {
            var decodeResult = Base64Url.TryDecode(chars, bytes, out var bytesWritten);
            Debug.Assert(decodeResult && bytesWritten == byteCount);
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        return lease;
    }

    private JoseSerializerOptions JoseSerializerOptions { get; }

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; }

    private IAlgorithmCollection AlgorithmCollection => AlgorithmCollectionProvider.Collection;

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSerializer"/> class.
    /// </summary>
    /// <param name="optionsAccessor">An accessor that provides <see cref="JoseSerializerOptions"/>.</param>
    /// <param name="algorithmCollectionProvider">An <see cref="IAlgorithmCollectionProvider"/> that provides a collection of <see cref="Algorithm"/> instances.</param>
    public JoseSerializer(IOptions<JoseSerializerOptions> optionsAccessor, IAlgorithmCollectionProvider algorithmCollectionProvider)
    {
        JoseSerializerOptions = optionsAccessor.Value;
        AlgorithmCollectionProvider = algorithmCollectionProvider;
    }

    private TAlgorithm AssertEnabled<TAlgorithm>(TAlgorithm algorithm)
        where TAlgorithm : Algorithm
    {
        var code = algorithm.Code;
        if (JoseSerializerOptions.DisabledAlgorithms.Contains(code))
            throw new JoseInvalidAlgorithmException($"The algorithm '{code}' is disabled.");

        return algorithm;
    }

    /// <inheritdoc />
    public CompactJwt ParseCompactJwt(string token)
    {
        var segments = token.SplitSegments('.');
        var protectionType = segments.Count switch
        {
            JwsSegmentCount => JoseProtectionTypes.Jws,
            JweSegmentCount => JoseProtectionTypes.Jwe,
            _ => throw new JoseException("The specified value does not represent a valid JOSE token in compact form.")
        };

        var deserializedHeader = DeserializeHeader(segments.First.Memory.Span);
        return new CompactJwt(protectionType, segments, deserializedHeader);
    }

    private static JsonElement DeserializeHeader(ReadOnlySpan<char> encodedHeader)
    {
        using var _ = DecodeBase64Url(encodedHeader, isSensitive: false, out var utf8Json);
        return JsonSerializer.Deserialize<JsonElement>(utf8Json);
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
            JoseProtectionTypes.Jws => DecodeJws(compactJwt, secretKey),
            JoseProtectionTypes.Jwe => DecodeJwe(compactJwt, secretKey),
            _ => throw new InvalidOperationException()
        };

    private static string DecodeUtf8(ReadOnlySequence<byte> byteSequence)
    {
        using var charBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        SecureEncoding.UTF8.GetChars(byteSequence, charBuffer);
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
            JoseProtectionTypes.Jws => DeserializeJws<T>(compactJwt, secretKey),
            JoseProtectionTypes.Jwe => DeserializeJwe<T>(compactJwt, secretKey),
            _ => throw new InvalidOperationException()
        };

    private T? Deserialize<T>(ReadOnlySequence<byte> byteSequence)
    {
        var jsonOptions = JoseSerializerOptions.JsonSerializerOptions;
        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = jsonOptions.AllowTrailingCommas,
            CommentHandling = jsonOptions.ReadCommentHandling,
            MaxDepth = jsonOptions.MaxDepth
        };
        var reader = new Utf8JsonReader(byteSequence, readerOptions);
        return JsonSerializer.Deserialize<T>(ref reader, jsonOptions);
    }

    /// <summary>
    /// Provides the ability to serialize a value to UTF8 using memory pooling.
    /// String values are encoded as UTF8 bytes.
    /// Objects are serialized as JSON.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The options to control JSON serialization behavior.</param>
    /// <param name="bytes">When this method returns, contains the UTF8 bytes from the serialized value.</param>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <returns>An <see cref="IDisposable"/> that controls the lifetime of the serialized bytes from a memory pool.</returns>
    public IDisposable SerializeToUtf8<T>(
        T value,
        JsonSerializerOptions? options,
        out ReadOnlySpan<byte> bytes)
    {
        if (value is string stringValue)
        {
            return Encode(SecureEncoding.UTF8, stringValue, out bytes);
        }

        var buffer = new Sequence<byte>(ArrayPool<byte>.Shared)
        {
            // increase our chances of getting a single-segment buffer
            MinimumSpanLength = 1024
        };
        try
        {
            using var writer = new Utf8JsonWriter(buffer);
            JsonSerializer.Serialize(writer, value, options ?? JoseSerializerOptions.JsonSerializerOptions);

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

    private IDisposable EncodeJose<T>(
        bool b64,
        T value,
        out ReadOnlySpan<char> chars)
    {
        using var bytesLease = SerializeToUtf8(value, options: null, out var bytes);
        return EncodeJose(b64, bytes, out chars);
    }

    private static bool TryEncodeJose(
        bool b64,
        ReadOnlySpan<byte> bytes,
        Span<char> chars,
        out int charsWritten)
    {
        if (b64)
        {
            return Base64Url.TryEncode(bytes, chars, out charsWritten);
        }

        charsWritten = SecureEncoding.UTF8.GetChars(bytes, chars);
        return charsWritten > 0;
    }

    private static IDisposable EncodeJose(
        bool b64,
        ReadOnlySpan<byte> bytes,
        out ReadOnlySpan<char> chars)
    {
        var charCount = b64 ?
            Base64Url.GetCharCountForEncode(bytes.Length) :
            SecureEncoding.UTF8.GetCharCount(bytes);

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

    private static IDisposable Encode(
        Encoding encoding,
        ReadOnlySpan<char> chars,
        out ReadOnlySpan<byte> bytes)
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

    private static void WriteCompactSegment(
        ReadOnlySpan<char> chars,
        IBufferWriter<char> writer,
        bool addDot = true)
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

    private static ReadOnlySpan<char> WriteCompactSegment(
        ReadOnlySpan<byte> bytes,
        IBufferWriter<char> writer,
        bool addDot = true,
        bool b64 = true)
    {
        var charCount = b64 ?
            Base64Url.GetCharCountForEncode(bytes.Length) :
            SecureEncoding.UTF8.GetCharCount(bytes);

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
