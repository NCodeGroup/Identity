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
using System.Text.Json;
using JetBrains.Annotations;
using NCode.CryptoMemory;
using NCode.Identity.Jose.Credentials;
using Nerdbank.Streams;

namespace NCode.Identity.Jose.Encoders;

/// <summary>
/// Provides an abstraction to encode a JOSE token.
/// </summary>
[PublicAPI]
public abstract class CommonJoseEncoder : JoseEncoder
{
    /// <summary>
    /// Gets the <see cref="JoseSerializer"/> instance.
    /// </summary>
    protected JoseSerializer JoseSerializer { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseEncoder"/> class.
    /// </summary>
    /// <param name="joseSerializer">The <see cref="JoseSerializer"/> instance.</param>
    protected CommonJoseEncoder(JoseSerializer joseSerializer)
    {
        JoseSerializer = joseSerializer;
    }

    /// <inheritdoc />
    public override string Encode<T>(
        T payload,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>();
        using var _ = JoseSerializer.SerializeToUtf8(
            payload,
            jsonOptions,
            out var payloadBytes);
        Encode(
            tokenBuffer,
            payloadBytes,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override void Encode<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        JsonSerializerOptions? jsonOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var _ = JoseSerializer.SerializeToUtf8(
            payload,
            jsonOptions,
            out var bytes);
        Encode(
            tokenWriter,
            bytes,
            extraHeaders);
    }

    /// <inheritdoc />
    public override string Encode(
        string payload,
        JoseSigningCredentials signingCredentials,
        JoseSigningOptions? signingOptions = null,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        Encode(
            tokenBuffer,
            payload.AsSpan(),
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override void Encode(
        IBufferWriter<char> tokenWriter,
        string payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        Encode(
            tokenWriter,
            payload.AsSpan(),
            extraHeaders);
    }

    /// <inheritdoc />
    public override string Encode(
        ReadOnlySpan<char> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        Encode(
            tokenBuffer,
            payload,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override void Encode(
        IBufferWriter<char> tokenWriter,
        ReadOnlySpan<char> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(payload);
        using var _ = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> payloadBytes);
        var bytesWritten = SecureEncoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);
        Encode(
            tokenWriter,
            payloadBytes,
            extraHeaders);
    }

    /// <inheritdoc />
    public override string Encode(
        ReadOnlySpan<byte> payload,
        IEnumerable<KeyValuePair<string, object>>? extraHeaders = null)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);
        Encode(
            tokenBuffer,
            payload,
            extraHeaders);
        return tokenBuffer.AsReadOnlySequence.ToString();
    }
}
