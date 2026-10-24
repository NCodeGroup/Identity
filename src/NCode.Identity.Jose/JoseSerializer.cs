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
using NCode.Disposables;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;
using Nerdbank.Streams;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides a default implementation for the <see cref="IJoseSerializer"/> interface.
/// </summary>
/// <param name="optionsAccessor">An accessor that provides <see cref="JoseSerializerOptions"/>.</param>
/// <param name="algorithmCollectionProvider">An <see cref="IAlgorithmCollectionProvider"/> that provides a collection of <see cref="Algorithm"/> instances.</param>
internal partial class JoseSerializer(
    IOptions<JoseSerializerOptions> optionsAccessor,
    IAlgorithmCollectionProvider algorithmCollectionProvider
) : IJoseSerializer
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
    public static IDisposable DecodeBase64Url(
        ReadOnlySpan<char> chars,
        bool isSensitive,
        out Span<byte> bytes
    )
    {
        var byteCount = Base64Url.GetByteCountForDecode(chars.Length);
        var lease = BufferFactory.Rent(byteCount, isSensitive, out bytes);
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

    private JoseSerializerOptions JoseSerializerOptions { get; } = optionsAccessor.Value;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private IAlgorithmCollection AlgorithmCollection => AlgorithmCollectionProvider.Collection;

    private TAlgorithm AssertEnabled<TAlgorithm>(TAlgorithm algorithm)
        where TAlgorithm : Algorithm
    {
        var code = algorithm.Code;
        if (JoseSerializerOptions.DisabledAlgorithms.Contains(code))
            throw new JoseInvalidAlgorithmException($"The algorithm '{code}' is disabled.");

        return algorithm;
    }

    /// <inheritdoc />
    public JoseToken Parse(string token)
    {
        if (IsJsonSerialized(token))
            return ParseJson(token);

        var tokenSpan = token.AsSpan();
        Span<Range> ranges = stackalloc Range[JweSegmentCount + 1];
        var count = tokenSpan.Split(ranges, '.');
        var protectionType = count switch
        {
            JwsSegmentCount => JoseProtectionTypes.Jws,
            JweSegmentCount => JoseProtectionTypes.Jwe,
            _ => throw new JoseException(
                "The specified value does not represent a valid JOSE token in compact form."
            ),
        };

        var segments = new ReadOnlyMemory<char>[count];
        for (var i = 0; i < count; i++)
            segments[i] = token.AsMemory(ranges[i]);

        var header = DeserializeHeader(segments[0].Span);
        return new CompactJwt(protectionType, header, token, segments);
    }

    private static JsonElement DeserializeHeader(ReadOnlySpan<char> encodedHeader)
    {
        using var _ = DecodeBase64Url(encodedHeader, isSensitive: false, out var utf8Json);
        return JsonElement.Parse(utf8Json);
    }

    /// <inheritdoc />
    public void Decode(JoseToken token, SecretKey secretKey, IBufferWriter<byte> payload)
    {
        switch (token)
        {
            case CompactJwt compactJwt:
                DecodeCompact(compactJwt, secretKey, payload);
                break;

            case JsonJwt jsonJwt:
                DecodeJson(jsonJwt, secretKey, payload);
                break;

            default:
                throw new JoseException(
                    $"The token type '{token.GetType().Name}' is not supported."
                );
        }
    }

    private void DecodeCompact(
        CompactJwt compactJwt,
        SecretKey secretKey,
        IBufferWriter<byte> payload
    )
    {
        switch (compactJwt.ProtectionType)
        {
            case JoseProtectionTypes.Jws:
                DecodeJws(compactJwt, secretKey, payload);
                break;

            case JoseProtectionTypes.Jwe:
                DecryptJwe(compactJwt, secretKey, payload);
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    /// <inheritdoc />
    public string Decode(JoseToken token, SecretKey secretKey)
    {
        using var payloadBuffer = new Sequence<byte>(ArrayPool<byte>.Shared);
        Decode(token, secretKey, payloadBuffer);
        return DecodeUtf8(payloadBuffer.AsReadOnlySequence);
    }

    /// <inheritdoc />
    public string Decode(string token, SecretKey secretKey) => Decode(Parse(token), secretKey);

    private static string DecodeUtf8(ReadOnlySequence<byte> byteSequence)
    {
        using var charBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        SecureEncoding.UTF8.GetChars(byteSequence, charBuffer);
        return charBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public T? Deserialize<T>(JoseToken token, SecretKey secretKey) =>
        Deserialize<T>(token, secretKey, jsonOptions: null);

    /// <inheritdoc />
    public T? Deserialize<T>(
        JoseToken token,
        SecretKey secretKey,
        JsonSerializerOptions? jsonOptions
    )
    {
        using var payloadBuffer = new Sequence<byte>(ArrayPool<byte>.Shared);
        Decode(token, secretKey, payloadBuffer);
        return Deserialize<T>(payloadBuffer.AsReadOnlySequence, jsonOptions);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string token, SecretKey secretKey) =>
        Deserialize<T>(Parse(token), secretKey, jsonOptions: null);

    /// <inheritdoc />
    public T? Deserialize<T>(
        string token,
        SecretKey secretKey,
        JsonSerializerOptions? jsonOptions
    ) => Deserialize<T>(Parse(token), secretKey, jsonOptions);

    private T? Deserialize<T>(
        ReadOnlySequence<byte> byteSequence,
        JsonSerializerOptions? jsonOptions
    )
    {
        var options = jsonOptions ?? JoseSerializerOptions.JsonSerializerOptions;
        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.ReadCommentHandling,
            MaxDepth = options.MaxDepth,
        };
        var reader = new Utf8JsonReader(byteSequence, readerOptions);
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    /// <inheritdoc />
    public void VerifyJws(JoseToken token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload)
    {
        switch (token)
        {
            case CompactJwt compactJwt:
                VerifyJwsCompact(compactJwt, secretKey, detachedPayload);
                break;

            case JsonJwt jsonJwt:
                VerifyJwsJson(jsonJwt, secretKey, detachedPayload);
                break;

            default:
                throw new JoseException(
                    $"The token type '{token.GetType().Name}' is not supported."
                );
        }
    }

    /// <inheritdoc />
    public void VerifyJws(JoseToken token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload)
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(detachedPayload);
        using var lease = BufferFactory.Rent(byteCount, isSensitive: false, out Span<byte> bytes);
        var bytesWritten = SecureEncoding.UTF8.GetBytes(detachedPayload, bytes);
        VerifyJws(token, secretKey, bytes[..bytesWritten]);
    }

    /// <inheritdoc />
    public void VerifyJws<T>(JoseToken token, SecretKey secretKey, T detachedPayload) =>
        VerifyJws(token, secretKey, detachedPayload, jsonOptions: null);

    /// <inheritdoc />
    public void VerifyJws<T>(
        JoseToken token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions
    )
    {
        using var _ = SerializeToUtf8(detachedPayload, jsonOptions, out var bytes);
        VerifyJws(token, secretKey, bytes);
    }

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<byte> detachedPayload) =>
        VerifyJws(Parse(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws(string token, SecretKey secretKey, ReadOnlySpan<char> detachedPayload) =>
        VerifyJws(Parse(token), secretKey, detachedPayload);

    /// <inheritdoc />
    public void VerifyJws<T>(string token, SecretKey secretKey, T detachedPayload) =>
        VerifyJws(Parse(token), secretKey, detachedPayload, jsonOptions: null);

    /// <inheritdoc />
    public void VerifyJws<T>(
        string token,
        SecretKey secretKey,
        T detachedPayload,
        JsonSerializerOptions? jsonOptions
    ) => VerifyJws(Parse(token), secretKey, detachedPayload, jsonOptions);

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
        out ReadOnlySpan<byte> bytes
    )
    {
        if (value is string stringValue)
        {
            return Encode(SecureEncoding.UTF8, stringValue, out bytes);
        }

        var buffer = new Sequence<byte>(ArrayPool<byte>.Shared)
        {
            // increase our chances of getting a single-segment buffer
            MinimumSpanLength = 1024,
        };

        try
        {
            using var writer = new Utf8JsonWriter(buffer);
            JsonSerializer.Serialize(
                writer,
                value,
                options ?? JoseSerializerOptions.JsonSerializerOptions
            );

            return buffer.ConsumeAsContiguousSpan(isSensitive: false, out bytes);
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static void EncodeJose(
        bool b64,
        ReadOnlySequence<byte> bytes,
        IBufferWriter<char> writer
    )
    {
        if (b64)
        {
            Base64Url.Encode(bytes, writer);
        }
        else
        {
            SecureEncoding.UTF8.GetChars(bytes, writer);
        }
    }

    private IDisposable EncodeJose<T>(bool b64, T value, out ReadOnlySpan<char> chars)
    {
        using var _ = SerializeToUtf8(value, options: null, out var bytes);
        return EncodeJose(b64, bytes, out chars);
    }

    private static IDisposable EncodeJose(
        bool b64,
        ReadOnlySpan<byte> bytes,
        out ReadOnlySpan<char> chars
    )
    {
        var byteCount = bytes.Length;
        if (byteCount == 0)
        {
            chars = ReadOnlySpan<char>.Empty;
            return Disposable.Empty;
        }

        var buffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: true);
        try
        {
            if (b64)
            {
                var charCount = Base64Url.GetCharCountForEncode(byteCount);
                var span = buffer.GetSpan(charCount);

                var result = Base64Url.TryEncode(bytes, span, out var charsWritten);
                buffer.Advance(charsWritten);
                Debug.Assert(result);

                chars = span[..charsWritten];
            }
            else
            {
                var charCount = SecureEncoding.UTF8.GetMaxCharCount(byteCount);
                var span = buffer.GetSpan(charCount);

                var charsWritten = SecureEncoding.UTF8.GetChars(bytes, span);
                buffer.Advance(charsWritten);

                chars = span[..charsWritten];
            }

            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static IDisposable Encode(
        Encoding encoding,
        ReadOnlySpan<char> chars,
        out ReadOnlySpan<byte> bytes
    )
    {
        var byteCount = encoding.GetByteCount(chars);
        var byteLease = BufferFactory.Rent(byteCount, isSensitive: false, out Span<byte> span);
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
        bool addDot = true
    )
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

    private static void WriteCompactSegment(
        ReadOnlySequence<byte> bytes,
        IBufferWriter<char> writer,
        bool addDot = true,
        bool b64 = true
    )
    {
        EncodeJose(b64, bytes, writer);

        if (addDot)
        {
            writer.GetSpan(1)[0] = '.';
            writer.Advance(1);
        }
    }

    private static ReadOnlySpan<char> WriteCompactSegment(
        ReadOnlySpan<byte> bytes,
        IBufferWriter<char> writer,
        bool addDot = true,
        bool b64 = true
    )
    {
        var byteCount = bytes.Length;
        var dotLength = addDot ? 1 : 0;

        int encodeCount;
        Span<char> chars;

        if (b64)
        {
            encodeCount = Base64Url.GetCharCountForEncode(byteCount);
            chars = writer.GetSpan(encodeCount + dotLength);

            var result = Base64Url.TryEncode(bytes, chars, out var charsWritten);
            Debug.Assert(result && charsWritten == encodeCount);
        }
        else
        {
            var maxCharCount = SecureEncoding.UTF8.GetMaxCharCount(byteCount);
            chars = writer.GetSpan(maxCharCount + dotLength);

            var charsWritten = SecureEncoding.UTF8.GetChars(bytes, chars);
            Debug.Assert(charsWritten <= maxCharCount);

            encodeCount = charsWritten;
        }

        if (addDot)
        {
            chars[encodeCount] = '.';
        }

        var totalLength = encodeCount + dotLength;
        writer.Advance(totalLength);
        return chars[..totalLength];
    }
}
