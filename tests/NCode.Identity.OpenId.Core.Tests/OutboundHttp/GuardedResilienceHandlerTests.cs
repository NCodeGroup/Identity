// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class GuardedResilienceHandlerTests
{
    private static GuardedResilienceHandler CreateHandler(
        StubHttpMessageHandler stub,
        OutboundHttpOptions options
    )
    {
        var pipelines = new GuardedResiliencePipelines(
            Microsoft.Extensions.Options.Options.Create(options)
        );
        return new GuardedResilienceHandler(pipelines) { InnerHandler = stub };
    }

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WhenAGetFailsWith503_RetriesIt()
    {
        var calls = 0;
        var stub = new StubHttpMessageHandler(
            (_, _) =>
            {
                var statusCode =
                    Interlocked.Increment(ref calls) == 1
                        ? HttpStatusCode.ServiceUnavailable
                        : HttpStatusCode.OK;
                return Task.FromResult(new HttpResponseMessage(statusCode));
            }
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenAGetIsRetried_DisposesTheFailedResponse()
    {
        using var failed = new TrackingResponse(HttpStatusCode.ServiceUnavailable);
        var calls = 0;
        var stub = new StubHttpMessageHandler(
            (_, _) =>
            {
                var response =
                    Interlocked.Increment(ref calls) == 1
                        ? failed
                        : new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult<HttpResponseMessage>(response);
            }
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(failed.IsDisposed);
    }

    [Fact]
    public async Task SendAsync_WhenAPostFailsWith503_DoesNotRetryIt()
    {
        var stub = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://client.example/notify"
        );

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenTheConnectionIsRefused_DoesNotRetryIt()
    {
        var stub = new StubHttpMessageHandler(
            (_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused")
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenRetriesAreOff_SendsAGetOnce()
    {
        var stub = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        );
        var options = new OutboundHttpOptions { MaxRetryAttempts = 0, RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenAPostAttemptHangs_ThrowsTaskCanceledWithTimeoutInner()
    {
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var options = new OutboundHttpOptions
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(50),
            RetryDelay = TimeSpan.Zero,
        };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://client.example/notify"
        );

        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenTheCallerCancels_DoesNotReportATimeout()
    {
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await invoker.SendAsync(request, cancellation.Token)
        );

        Assert.IsNotType<TimeoutException>(exception.InnerException);
    }

    #endregion

    #region Scaffolding Types

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback
    ) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _callCount);
            return callback(request, cancellationToken);
        }
    }

    private sealed class TrackingResponse(HttpStatusCode statusCode)
        : HttpResponseMessage(statusCode)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    #endregion
}
