// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class DefaultRegistrationTests
{
    private sealed class CountingHttpMessageHandler(
        Func<CancellationToken, Task<HttpResponseMessage>> callback
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
            return callback(cancellationToken);
        }
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        new ServiceBuilder<OpenIdAuthenticationLibrary>(services).AddOutboundHttpServices();
        return services;
    }

    private static HttpClient CreateClient(
        CountingHttpMessageHandler stub,
        out ServiceProvider serviceProvider
    )
    {
        var services = CreateServices();
        services.Configure<OutboundHttpOptions>(options =>
        {
            options.RetryDelay = TimeSpan.Zero;
            options.MaxRetryAttempts = 2;
            options.AttemptTimeout = TimeSpan.FromMilliseconds(50);
        });
        services
            .AddHttpClient(GuardedHttpClient.Name)
            .ConfigurePrimaryHttpMessageHandler(() => stub);

        serviceProvider = services.BuildServiceProvider();
        return serviceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(GuardedHttpClient.Name);
    }

    private static Task<HttpResponseMessage> ServiceUnavailable(CancellationToken _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

    #region AddOutboundHttpServices Tests

    [Fact]
    public async Task AddOutboundHttpServices_GuardedClient_PinsConnectionsAndRefusesRedirects()
    {
        var services = CreateServices();
        await using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IHttpMessageHandlerFactory>();

        var handler = factory.CreateHandler(GuardedHttpClient.Name);

        var hasTimeoutHandler = false;
        var current = handler;
        while (true)
        {
            if (current is GuardedHttpTimeoutHandler)
                hasTimeoutHandler = true;

            if (current is not DelegatingHandler { InnerHandler: { } inner })
                break;

            current = inner;
        }

        var socketsHandler = Assert.IsType<SocketsHttpHandler>(current);
        Assert.False(socketsHandler.AllowAutoRedirect);
        Assert.NotNull(socketsHandler.ConnectCallback);
        Assert.True(hasTimeoutHandler);
    }

    [Fact]
    public async Task AddOutboundHttpServices_WhenAGetFailsWith503_RetriesTwice()
    {
        var stub = new CountingHttpMessageHandler(ServiceUnavailable);
        using var client = CreateClient(stub, out var serviceProvider);
        await using var _ = serviceProvider;

        using var response = await client.GetAsync("https://example.test/", CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, stub.CallCount);
    }

    [Fact]
    public async Task AddOutboundHttpServices_WhenAPostFailsWith503_DoesNotRetry()
    {
        var stub = new CountingHttpMessageHandler(ServiceUnavailable);
        using var client = CreateClient(stub, out var serviceProvider);
        await using var _ = serviceProvider;
        using var content = new ByteArrayContent([1, 2, 3]);

        using var response = await client.PostAsync(
            "https://example.test/",
            content,
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task AddOutboundHttpServices_WhenAnAttemptHangs_ThrowsTaskCanceledWithTimeoutInner()
    {
        var stub = new CountingHttpMessageHandler(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(stub, out var serviceProvider);
        await using var _ = serviceProvider;

        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await client.GetAsync("https://example.test/", CancellationToken.None)
        );

        Assert.IsType<TimeoutException>(exception.InnerException);
    }

    #endregion
}
