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
    public override async ValueTask<string> EncodeAsync<T>(
        T payload,
        CancellationToken cancellationToken
    )
    {
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

        await EncodeAsync(tokenBuffer, payload, cancellationToken);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override async ValueTask EncodeAsync<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        CancellationToken cancellationToken
    )
    {
        using var _ = SerializePayload(payload, out var payloadBytes);

        await EncodeAsync(tokenWriter, payloadBytes, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask<string> EncodeAsync(
        string payload,
        CancellationToken cancellationToken
    )
    {
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

        await EncodeAsync(tokenBuffer, payload.AsMemory(), cancellationToken);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override ValueTask EncodeAsync(
        IBufferWriter<char> tokenWriter,
        string payload,
        CancellationToken cancellationToken
    ) => EncodeAsync(tokenWriter, payload.AsMemory(), cancellationToken);

    /// <inheritdoc />
    public override async ValueTask<string> EncodeAsync(
        ReadOnlyMemory<char> payload,
        CancellationToken cancellationToken
    )
    {
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

        await EncodeAsync(tokenBuffer, payload, cancellationToken);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    /// <inheritdoc />
    public override async ValueTask EncodeAsync(
        IBufferWriter<char> tokenWriter,
        ReadOnlyMemory<char> payload,
        CancellationToken cancellationToken
    )
    {
        using var _ = EncodeUtf8(payload.Span, out var payloadBytes);

        await EncodeAsync(tokenWriter, payloadBytes, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask<string> EncodeAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    )
    {
        using var tokenBuffer = BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

        await EncodeAsync(tokenBuffer, payload, cancellationToken);

        return tokenBuffer.AsReadOnlySequence.ToString();
    }

    private IDisposable SerializePayload<T>(T payload, out ReadOnlyMemory<byte> bytes)
    {
        using var serialized = JoseSerializer.SerializeToUtf8(
            payload,
            PayloadJsonOptions,
            out var serializedBytes
        );

        var lease = BufferFactory.Rent(
            serializedBytes.Length,
            isSensitive: true,
            out Memory<byte> memory
        );
        serializedBytes.CopyTo(memory.Span);

        bytes = memory;
        return lease;
    }

    private static IDisposable EncodeUtf8(
        ReadOnlySpan<char> payload,
        out ReadOnlyMemory<byte> bytes
    )
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(payload);
        var lease = BufferFactory.Rent(byteCount, isSensitive: false, out Memory<byte> memory);

        var bytesWritten = SecureEncoding.UTF8.GetBytes(payload, memory.Span);
        Debug.Assert(bytesWritten == byteCount);

        bytes = memory;
        return lease;
    }
}
