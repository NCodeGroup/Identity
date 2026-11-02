// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class GuardedResilienceHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan EnteredDeadline = TimeSpan.FromSeconds(10);

    private static Task<HttpResponseMessage> RespondServiceUnavailable(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        return Task.FromResult(response);
    }

    private FakeTimeProvider Time { get; } = new(Start);

    private GuardedResilienceHandler CreateHandler(
        StubHttpMessageHandler stub,
        OutboundHttpOptions options
    )
    {
        var wrappedOptions = Microsoft.Extensions.Options.Options.Create(options);
        var pipelines = new GuardedResiliencePipelines(wrappedOptions, Time);
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
                var response = new HttpResponseMessage(statusCode);
                return Task.FromResult(response);
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
    public async Task SendAsync_WhenAGetFailsOnEveryAttempt_ReturnsTheLastResponse()
    {
        List<TrackingResponse> responses = [];
        var stub = new StubHttpMessageHandler(
            (_, _) =>
            {
                var response = new TrackingResponse(HttpStatusCode.ServiceUnavailable);
                responses.Add(response);
                return Task.FromResult<HttpResponseMessage>(response);
            }
        );
        var options = new OutboundHttpOptions { MaxRetryAttempts = 2, RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(options.MaxRetryAttempts + 1, stub.CallCount);
        Assert.Same(responses[^1], response);
        Assert.False(responses[^1].IsDisposed);
        Assert.All(responses[..^1], failed => Assert.True(failed.IsDisposed));
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
        var stub = new StubHttpMessageHandler(RespondServiceUnavailable);
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
        var stub = new StubHttpMessageHandler(RespondServiceUnavailable);
        var options = new OutboundHttpOptions { MaxRetryAttempts = 0, RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenAGetAttemptTimesOut_RetriesIt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                if (Interlocked.Increment(ref calls) > 1)
                    return new HttpResponseMessage(HttpStatusCode.OK);

                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        var sending = invoker.SendAsync(request, CancellationToken.None);
        await entered.Task.WaitAsync(EnteredDeadline);
        Time.Advance(options.AttemptTimeout);
        using var response = await sending;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenAPostAttemptHangs_ThrowsTaskCanceledWithTimeoutInner()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://client.example/notify"
        );

        var sending = invoker.SendAsync(request, CancellationToken.None);
        await entered.Task.WaitAsync(EnteredDeadline);
        Time.Advance(options.AttemptTimeout);
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => sending);

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenTheCallerCancels_DoesNotReportATimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                await cancellation.CancelAsync();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await invoker.SendAsync(request, cancellation.Token)
        );

        Assert.IsNotType<TimeoutException>(exception.InnerException);
        Assert.Equal(1, stub.CallCount);
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
