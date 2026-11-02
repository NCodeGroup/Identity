// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using NCode.Buffers;

namespace NCode.Identity.Jose.Encoders;

/// <summary>
/// Provides an abstraction to encode a JOSE token.
/// </summary>
/// <param name="joseSerializer">The <see cref="JoseSerializer"/> instance.</param>
internal abstract class CommonJoseEncoder(JoseSerializer joseSerializer) : JoseEncoder
{
    /// <summary>
    /// Gets the <see cref="JoseSerializer"/> instance.
    /// </summary>
    protected JoseSerializer JoseSerializer { get; } = joseSerializer;

    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used to serialize a strongly-typed payload.
    /// </summary>
    protected abstract JsonSerializerOptions? PayloadJsonOptions { get; }

    /// <inheritdoc />
    public override string Encode<T>(T payload)
    {
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

        using var _ = JoseSerializer.SerializeToUtf8(
            payload,
            PayloadJsonOptions,
            out var payloadBytes
        );

        Encode(tokenBuffer, payloadBytes);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override void Encode<T>(IBufferWriter<char> tokenWriter, T payload)
    {
        using var _ = JoseSerializer.SerializeToUtf8(payload, PayloadJsonOptions, out var bytes);

        Encode(tokenWriter, bytes);
    }

    /// <inheritdoc />
    public override string Encode(string payload)
    {
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

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
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

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
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

        Encode(tokenBuffer, payload);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }
}
