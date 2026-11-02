// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Metrics;
using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Http;

public class ProviderResilienceHandlerTests : BaseTests
{
    private TestMetrics Metrics { get; } = new();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var collector in Collectors.Values)
                collector.Dispose();
            Metrics.Dispose();
        }
        base.Dispose(disposing);
    }

    private List<string?> Measured(string instrumentName, string attributeName) =>
        CreateCollector(instrumentName)
            .GetMeasurementSnapshot()
            .Select(measurement => measurement.Tags.GetValueOrDefault(attributeName) as string)
            .ToList();

    private Dictionary<string, MetricCollector<long>> Collectors { get; } = [];

    private MetricCollector<long> CreateCollector(string instrumentName)
    {
        if (!Collectors.TryGetValue(instrumentName, out var collector))
        {
            collector = new MetricCollector<long>(
                Metrics.MeterFactory,
                OpenIdClientMetrics.MeterName,
                instrumentName
            );
            Collectors[instrumentName] = collector;
        }

        return collector;
    }

    private ProviderResilienceHandler CreateHandler(
        HttpMessageHandler innerHandler,
        OpenIdClientOptions? options = null
    )
    {
        var clientOptions = options ?? new OpenIdClientOptions { RetryDelay = TimeSpan.Zero };
        var wrappedOptions = Microsoft.Extensions.Options.Options.Create(clientOptions);
        // The collectors subscribe before the pipelines create their instruments, so they see every measurement.
        CreateCollector(ProviderMetrics.RetriesName);
        CreateCollector(ProviderMetrics.TimeoutsName);
        var pipelines = new ProviderResiliencePipelines(
            wrappedOptions,
            Metrics.CreateProviderMetrics()
        );
        return new ProviderResilienceHandler(pipelines) { InnerHandler = innerHandler };
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode) =>
        StubHttpMessageHandler.CreateResponse(statusCode, "{}");

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WhenAGetFailsTransiently_RetriesIt()
    {
        var unavailable = CreateResponse(HttpStatusCode.ServiceUnavailable);
        var ok = CreateResponse(HttpStatusCode.OK);
        var stub = StubHttpMessageHandler.Sequence(unavailable, ok);
        using var handler = CreateHandler(stub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal<string?>(
            ["503"],
            Measured(ProviderMetrics.RetriesName, ProviderMetrics.ErrorTypeAttribute)
        );
    }

    [Fact]
    public async Task SendAsync_WhenAGetIsRetried_DisposesTheFailedResponse()
    {
        using var failed = new TrackingResponse(HttpStatusCode.ServiceUnavailable);
        var ok = CreateResponse(HttpStatusCode.OK);
        var stub = StubHttpMessageHandler.Sequence(failed, ok);
        using var handler = CreateHandler(stub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(failed.IsDisposed);
    }

    [Fact]
    public async Task SendAsync_WhenAPostFails_DoesNotRetryIt()
    {
        var unavailable = CreateResponse(HttpStatusCode.ServiceUnavailable);
        var stub = StubHttpMessageHandler.Sequence(unavailable);
        using var handler = CreateHandler(stub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://op.example/token");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single(stub.Requests);
        Assert.Empty(Measured(ProviderMetrics.RetriesName, ProviderMetrics.ErrorTypeAttribute));
    }

    [Fact]
    public async Task SendAsync_WhenAGetCarriesADPoPProof_DoesNotRetryIt()
    {
        var unavailable = CreateResponse(HttpStatusCode.ServiceUnavailable);
        var stub = StubHttpMessageHandler.Sequence(unavailable);
        using var handler = CreateHandler(stub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/userinfo");
        request.Headers.Add(OpenIdProtocol.DPoP.HeaderName, "proof");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task SendAsync_WhenAGetIsNotFound_DoesNotRetryIt()
    {
        var notFound = CreateResponse(HttpStatusCode.NotFound);
        var stub = StubHttpMessageHandler.Sequence(notFound);
        using var handler = CreateHandler(stub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task SendAsync_WhenRetriesAreOff_DoesNotRetry()
    {
        var unavailable = CreateResponse(HttpStatusCode.ServiceUnavailable);
        var stub = StubHttpMessageHandler.Sequence(unavailable);
        var options = new OpenIdClientOptions { MaxRetryAttempts = 0, RetryDelay = TimeSpan.Zero };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task SendAsync_WhenAnAttemptTimesOut_RetriesIt()
    {
        var calls = 0;
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);

                return CreateResponse(HttpStatusCode.OK);
            }
        );
        var options = new OpenIdClientOptions
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(50),
            TotalTimeout = TimeSpan.FromSeconds(5),
            RetryDelay = TimeSpan.Zero,
        };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal<string?>(
            ["timeout"],
            Measured(ProviderMetrics.RetriesName, ProviderMetrics.ErrorTypeAttribute)
        );
        Assert.Equal<string?>(
            ["attempt"],
            Measured(ProviderMetrics.TimeoutsName, ProviderMetrics.TimeoutScopeAttribute)
        );
    }

    [Fact]
    public async Task SendAsync_WhenTheTotalTimeoutPasses_ThrowsTaskCanceledWithTimeoutInner()
    {
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return CreateResponse(HttpStatusCode.OK);
            }
        );
        var options = new OpenIdClientOptions
        {
            TotalTimeout = TimeSpan.FromMilliseconds(100),
            RetryDelay = TimeSpan.Zero,
        };
        using var handler = CreateHandler(stub, options);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://op.example/token");

        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.Equal<string?>(
            ["total"],
            Measured(ProviderMetrics.TimeoutsName, ProviderMetrics.TimeoutScopeAttribute)
        );
    }

    [Fact]
    public async Task SendAsync_WhenTheCallerCancels_DoesNotReportATimeout()
    {
        var stub = new StubHttpMessageHandler(
            async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return CreateResponse(HttpStatusCode.OK);
            }
        );
        using var handler = CreateHandler(stub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await invoker.SendAsync(request, cancellation.Token)
        );

        Assert.IsNotType<TimeoutException>(exception.InnerException);
        Assert.Empty(Measured(ProviderMetrics.TimeoutsName, ProviderMetrics.TimeoutScopeAttribute));
    }

    #endregion

    #region Scaffolding Types

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
