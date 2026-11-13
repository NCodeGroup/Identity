// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Core.Tests.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class BackChannelLogoutDeliveryJobHandlerTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string LogoutUri = "https://rp.example/backchannel-logout";
    private const string LogoutToken = "header.payload.signature";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private RecordingHttpMessageHandler Handler { get; }
    private FakeLogger<BackChannelLogoutDeliveryJobHandler> Logger { get; } = new();
    private BackChannelLogoutDeliveryJobHandler Subject { get; }

    public BackChannelLogoutDeliveryJobHandlerTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        Handler = new RecordingHttpMessageHandler();
        Subject = new BackChannelLogoutDeliveryJobHandler(MockHttpClientFactory.Object, Logger);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }

    private sealed class DelayingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static DispatchContext Context() =>
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    private static BackChannelLogoutDeliveryJob CreateJob(string logoutUri = LogoutUri) =>
        new()
        {
            ClientId = ClientId,
            LogoutUri = logoutUri,
            LogoutToken = LogoutToken,
        };

    private void AssertDeliveryFailedReason(string reason)
    {
        var record = Assert.Single(
            Logger.Collector.GetSnapshot(),
            r => r.Id.Id == EventIds.BackChannelLogoutDeliveryFailed
        );
        Assert.Equal(reason, record.GetStructuredStateValue("Reason"));
        Assert.DoesNotContain(LogoutToken, record.Message);
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenConfigured_PostsFormEncodedLogoutToken()
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(Handler))
            .Verifiable();

        await Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None);

        Assert.Equal(new Uri(LogoutUri), Handler.RequestUri);
        Assert.NotNull(Handler.RequestBody);
        Assert.Contains(OpenIdProtocol.Parameters.LogoutToken, Handler.RequestBody);
        Assert.Contains(LogoutToken, Handler.RequestBody);
    }

    [Fact]
    public async Task HandleAsync_WhenTheResponseHasABody_DoesNotReadIt()
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(new UnreadableBodyHttpMessageHandler()))
            .Verifiable();

        await Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointReturnsError_Throws()
    {
        var errorHandler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(errorHandler))
            .Verifiable();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointReturnsError_LogsHttpStatus()
    {
        var errorHandler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(errorHandler))
            .Verifiable();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None).AsTask()
        );

        AssertDeliveryFailedReason("http_status");
    }

    [Fact]
    public async Task HandleAsync_WhenTheHostResolvesToARefusedAddress_LogsBlockedAddress()
    {
        using var handler = new SocketsHttpHandler
        {
            ConnectCallback = new DefaultOutboundHttpForgeryGuard([]).ConnectAsync,
        };
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Subject
                .HandleAsync(
                    CreateJob("https://10.0.0.5/backchannel-logout"),
                    Context(),
                    CancellationToken.None
                )
                .AsTask()
        );

        AssertDeliveryFailedReason("blocked_address");
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerCancels_ThrowsWithoutAFailureLog()
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(new DelayingHttpMessageHandler()))
            .Verifiable();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Subject.HandleAsync(CreateJob(), Context(), cts.Token).AsTask()
        );

        Assert.DoesNotContain(
            Logger.Collector.GetSnapshot(),
            r => r.Id.Id == EventIds.BackChannelLogoutDeliveryFailed
        );
    }

    #endregion
}
