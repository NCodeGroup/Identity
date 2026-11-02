// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

/// <summary>
/// A response body that fails every read, so a test proves that the code under test never reads it.
/// </summary>
internal sealed class UnreadableStream : Stream
{
    private const string ReadMessage = "The response body must not be read.";

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException(ReadMessage);

    public override int Read(Span<byte> buffer) => throw new InvalidOperationException(ReadMessage);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => throw new InvalidOperationException(ReadMessage);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
