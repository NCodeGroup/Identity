// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Polly.Timeout;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class GuardedHttpTimeoutHandlerTests
{
    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => callback(request, cancellationToken);
    }

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WhenTheInnerHandlerTimesOut_ThrowsTaskCanceledWithTimeoutInner()
    {
        using var handler = new GuardedHttpTimeoutHandler
        {
            InnerHandler = new StubHttpMessageHandler(
                (_, _) =>
                    throw new TimeoutRejectedException("timed out", TimeSpan.FromMilliseconds(50))
            ),
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.IsType<TimeoutException>(exception.InnerException);
    }

    [Fact]
    public async Task SendAsync_WhenTheInnerHandlerSucceeds_ReturnsItsResponse()
    {
        using var expected = new HttpResponseMessage(HttpStatusCode.Accepted);
        using var handler = new GuardedHttpTimeoutHandler
        {
            InnerHandler = new StubHttpMessageHandler((_, _) => Task.FromResult(expected)),
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

        var actual = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Same(expected, actual);
    }

    #endregion
}
