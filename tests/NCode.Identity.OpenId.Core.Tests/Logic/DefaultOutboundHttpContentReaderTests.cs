#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using System.Net;
using System.Text;
using NCode.Identity.OpenId.Authentication.Logic;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Logic;

public class DefaultOutboundHttpContentReaderTests : BaseTests
{
    private DefaultOutboundHttpContentReader Subject { get; } = new();

    private static HttpResponseMessage StringResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };

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
        Assert.Equal("hello", Encoding.UTF8.GetString(destination.WrittenSpan));
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
            Content = new UnknownLengthContent(Encoding.UTF8.GetBytes("0123456789")),
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
