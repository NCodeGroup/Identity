// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

/// <summary>
/// A readable stream whose every read throws <paramref name="exception"/>, so a test proves how a fetch handles a
/// response body that fails while it is read or decompressed.
/// </summary>
/// <param name="exception">The exception that every read throws.</param>
internal sealed class FailingReadStream(Exception exception) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => ValueTask.FromException<int>(exception);

    public override int Read(byte[] buffer, int offset, int count) => throw exception;

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
