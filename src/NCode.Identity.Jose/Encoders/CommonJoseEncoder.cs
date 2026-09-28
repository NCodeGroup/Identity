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
using JetBrains.Annotations;
using NCode.Buffers;
using Nerdbank.Streams;

namespace NCode.Identity.Jose.Encoders;

/// <summary>
/// Provides an abstraction to encode a JOSE token.
/// </summary>
/// <param name="joseSerializer">The <see cref="JoseSerializer"/> instance.</param>
[PublicAPI]
internal abstract class CommonJoseEncoder(JoseSerializer joseSerializer) : JoseEncoder
{
    /// <summary>
    /// Gets the <see cref="JoseSerializer"/> instance.
    /// </summary>
    protected JoseSerializer JoseSerializer { get; } = joseSerializer;

    /// <summary>
    /// Gets the <see cref="JoseEncodingOptions"/> that this encoder was created with.
    /// </summary>
    protected abstract JoseEncodingOptions EncodingOptions { get; }

    /// <inheritdoc />
    public override string Encode<T>(T payload)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);

        using var _ = JoseSerializer.SerializeToUtf8(
            payload,
            EncodingOptions.JsonOptions,
            out var payloadBytes
        );

        Encode(tokenBuffer, payloadBytes);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override void Encode<T>(IBufferWriter<char> tokenWriter, T payload)
    {
        using var _ = JoseSerializer.SerializeToUtf8(
            payload,
            EncodingOptions.JsonOptions,
            out var bytes
        );

        Encode(tokenWriter, bytes);
    }

    /// <inheritdoc />
    public override string Encode(string payload)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);

        Encode(tokenBuffer, payload.AsSpan());

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override void Encode(IBufferWriter<char> tokenWriter, string payload)
    {
        Encode(tokenWriter, payload.AsSpan());
    }

    /// <inheritdoc />
    public override string Encode(ReadOnlySpan<char> payload)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);

        Encode(tokenBuffer, payload);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override void Encode(IBufferWriter<char> tokenWriter, ReadOnlySpan<char> payload)
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(payload);
        using var _ = BufferFactory.Rent(
            byteCount,
            isSensitive: false,
            out Span<byte> payloadBytes
        );

        var bytesWritten = SecureEncoding.UTF8.GetBytes(payload, payloadBytes);
        Debug.Assert(bytesWritten == byteCount);

        Encode(tokenWriter, payloadBytes);
    }

    /// <inheritdoc />
    public override string Encode(ReadOnlySpan<byte> payload)
    {
        using var tokenBuffer = new Sequence<char>(ArrayPool<char>.Shared);

        Encode(tokenBuffer, payload);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }
}
