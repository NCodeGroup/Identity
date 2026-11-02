// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Net;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public class DefaultOutboundHttpContentReaderTests : BaseTests
{
    private DefaultOutboundHttpContentReader Subject { get; } = new();

    private static HttpResponseMessage StringResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, SecureEncoding.UTF8) };

    #region ReadCappedAsync Tests

    [Fact]
    public async Task ReadCappedAsync_WhenWithinCap_WritesBodyAndCompletes()
    {
        using var response = StringResponse("hello");
        var destination = new ArrayBufferWriter<byte>();

        var result = await Subject.ReadCappedAsync(
            response,
            destination,
            maxSizeBytes: 1024,
            CancellationToken.None
        );

        Assert.Equal(OutboundHttpReadResult.Completed, result);
        Assert.Equal("hello", SecureEncoding.UTF8.GetString(destination.WrittenSpan));
    }

    [Fact]
    public async Task ReadCappedAsync_WhenContentLengthExceedsCap_ReturnsExceededWithoutWriting()
    {
        using var response = StringResponse("0123456789");
        var destination = new ArrayBufferWriter<byte>();

        var result = await Subject.ReadCappedAsync(
            response,
            destination,
            maxSizeBytes: 4,
            CancellationToken.None
        );

        Assert.Equal(OutboundHttpReadResult.Exceeded, result);
        Assert.Equal(0, destination.WrittenCount);
    }

    [Fact]
    public async Task ReadCappedAsync_WhenStreamedBodyExceedsCapWithUnknownLength_ReturnsExceeded()
    {
        // No advertised Content-Length, so the early reject is bypassed and the streaming cap is exercised.
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(SecureEncoding.UTF8.GetBytes("0123456789")),
        };
        var destination = new ArrayBufferWriter<byte>();

        var result = await Subject.ReadCappedAsync(
            response,
            destination,
            maxSizeBytes: 4,
            CancellationToken.None
        );

        Assert.Equal(OutboundHttpReadResult.Exceeded, result);
    }

    #endregion

    private sealed class UnknownLengthContent(byte[] payload) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(payload, 0, payload.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
