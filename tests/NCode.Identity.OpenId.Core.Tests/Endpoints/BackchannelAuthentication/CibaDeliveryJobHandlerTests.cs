// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Core.Tests.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class CibaDeliveryJobHandlerTests : BaseTests
{
    private const string AuthReqId = "auth-req-id";
    private const string NotificationToken = "notify-token";
    private const string Endpoint = "https://client.test/ciba";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private RecordingHttpMessageHandler Handler { get; }
    private FakeLogger<CibaDeliveryJobHandler> Logger { get; } = new();
    private CibaDeliveryJobHandler Subject { get; }

    public CibaDeliveryJobHandlerTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        Handler = new RecordingHttpMessageHandler();
        Subject = new CibaDeliveryJobHandler(MockHttpClientFactory.Object, Logger);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
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

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromException<HttpResponseMessage>(exception);
    }

    private void SetupHttpClient() =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(Handler))
            .Verifiable();

    private static DispatchContext Context() =>
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    private static CibaDeliveryJob CreatePingJob(string endpointUrl = Endpoint) =>
        new()
        {
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            ClientId = "client-1",
            EndpointUrl = endpointUrl,
            ClientNotificationToken = NotificationToken,
            AuthReqId = AuthReqId,
        };

    private void AssertDeliveryFailedReason(string reason)
    {
        var record = Assert.Single(
            Logger.Collector.GetSnapshot(),
            r => r.Id.Id == EventIds.CibaDeliveryFailed
        );
        Assert.Equal(reason, record.GetStructuredStateValue("Reason"));
        Assert.DoesNotContain(NotificationToken, record.Message);
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTheResponseHasABody_DoesNotReadIt()
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(new UnreadableBodyHttpMessageHandler()))
            .Verifiable();

        var job = new CibaDeliveryJob
        {
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            ClientId = "client-1",
            EndpointUrl = Endpoint,
            ClientNotificationToken = NotificationToken,
            AuthReqId = AuthReqId,
        };

        await Subject.HandleAsync(job, Context(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenPing_PostsAuthReqIdWithBearerToken()
    {
        SetupHttpClient();

        var job = new CibaDeliveryJob
        {
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            ClientId = "client-1",
            EndpointUrl = Endpoint,
            ClientNotificationToken = NotificationToken,
            AuthReqId = AuthReqId,
        };

        await Subject.HandleAsync(job, Context(), CancellationToken.None);

        Assert.Equal(new Uri(Endpoint), Handler.RequestUri);
        Assert.Equal($"Bearer {NotificationToken}", Handler.Authorization);

        var jsonElement = JsonElement.Parse(Handler.RequestBody!);
        var root = jsonElement;
        Assert.Equal(AuthReqId, root.GetProperty(OpenIdProtocol.Parameters.AuthReqId).GetString());
        Assert.False(root.TryGetProperty(OpenIdProtocol.Parameters.AccessToken, out _));
    }

    [Fact]
    public async Task HandleAsync_WhenPush_PostsTokenResponse()
    {
        SetupHttpClient();

        var job = new CibaDeliveryJob
        {
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
            ClientId = "client-1",
            EndpointUrl = Endpoint,
            ClientNotificationToken = NotificationToken,
            AuthReqId = AuthReqId,
            AccessToken = "access-token-value",
            TokenType = OpenIdProtocol.TokenTypes.Bearer,
            ExpiresInSeconds = 3600,
            IdToken = "id-token-value",
            Scope = "openid api",
        };

        await Subject.HandleAsync(job, Context(), CancellationToken.None);

        var jsonElement = JsonElement.Parse(Handler.RequestBody!);
        var root = jsonElement;
        Assert.Equal(AuthReqId, root.GetProperty(OpenIdProtocol.Parameters.AuthReqId).GetString());
        Assert.Equal(
            "access-token-value",
            root.GetProperty(OpenIdProtocol.Parameters.AccessToken).GetString()
        );
        Assert.Equal(3600, root.GetProperty(OpenIdProtocol.Parameters.ExpiresIn).GetInt64());
        Assert.Equal(
            "id-token-value",
            root.GetProperty(OpenIdProtocol.Parameters.IdToken).GetString()
        );
        Assert.Equal("openid api", root.GetProperty(OpenIdProtocol.Parameters.Scope).GetString());
        Assert.False(root.TryGetProperty(OpenIdProtocol.Parameters.RefreshToken, out _));
        Assert.False(root.TryGetProperty(OpenIdProtocol.Parameters.RefreshTokenExpiresIn, out _));
    }

    [Fact]
    public async Task HandleAsync_WhenPushWithRefreshToken_PostsTheRefreshTokenAndItsLineageLifetime()
    {
        SetupHttpClient();

        var job = new CibaDeliveryJob
        {
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
            ClientId = "client-1",
            EndpointUrl = Endpoint,
            ClientNotificationToken = NotificationToken,
            AuthReqId = AuthReqId,
            AccessToken = "access-token-value",
            TokenType = OpenIdProtocol.TokenTypes.Bearer,
            ExpiresInSeconds = 3600,
            RefreshToken = "refresh-token-value",
            RefreshTokenExpiresInSeconds = 2_592_000,
        };

        await Subject.HandleAsync(job, Context(), CancellationToken.None);

        var jsonElement = JsonElement.Parse(Handler.RequestBody!);
        var root = jsonElement;
        Assert.Equal(
            "refresh-token-value",
            root.GetProperty(OpenIdProtocol.Parameters.RefreshToken).GetString()
        );
        Assert.Equal(
            2_592_000,
            root.GetProperty(OpenIdProtocol.Parameters.RefreshTokenExpiresIn).GetInt64()
        );
    }

    [Fact]
    public async Task HandleAsync_WhenPushError_PostsTheErrorPayload()
    {
        SetupHttpClient();

        var job = new CibaDeliveryJob
        {
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
            ClientId = "client-1",
            EndpointUrl = Endpoint,
            ClientNotificationToken = NotificationToken,
            AuthReqId = AuthReqId,
            Error = OpenIdProtocol.ErrorCodes.AccessDenied,
        };

        await Subject.HandleAsync(job, Context(), CancellationToken.None);

        var jsonElement = JsonElement.Parse(Handler.RequestBody!);
        var root = jsonElement;
        Assert.Equal(AuthReqId, root.GetProperty(OpenIdProtocol.Parameters.AuthReqId).GetString());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.AccessDenied,
            root.GetProperty(OpenIdProtocol.Parameters.ErrorCode).GetString()
        );
        Assert.False(root.TryGetProperty(OpenIdProtocol.Parameters.AccessToken, out _));
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointReturnsError_Throws()
    {
        var errorHandler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        MockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(errorHandler))
            .Verifiable();

        var job = new CibaDeliveryJob
        {
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            ClientId = "client-1",
            EndpointUrl = Endpoint,
            ClientNotificationToken = NotificationToken,
            AuthReqId = AuthReqId,
        };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Subject.HandleAsync(job, Context(), CancellationToken.None).AsTask()
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
            Subject.HandleAsync(CreatePingJob(), Context(), CancellationToken.None).AsTask()
        );

        AssertDeliveryFailedReason("http_status");
    }

    [Fact]
    public async Task HandleAsync_WhenTheSendFailsWithAnIOException_LogsConnectionAndRethrows()
    {
        var failure = new IOException("The connection was reset.");
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(new ThrowingHttpMessageHandler(failure)))
            .Verifiable();

        var thrown = await Assert.ThrowsAsync<IOException>(() =>
            Subject.HandleAsync(CreatePingJob(), Context(), CancellationToken.None).AsTask()
        );

        Assert.Same(failure, thrown);
        AssertDeliveryFailedReason("connection");
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
                    CreatePingJob("https://10.0.0.5/ciba"),
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
            Subject.HandleAsync(CreatePingJob(), Context(), cts.Token).AsTask()
        );

        Assert.DoesNotContain(
            Logger.Collector.GetSnapshot(),
            r => r.Id.Id == EventIds.CibaDeliveryFailed
        );
    }

    #endregion
}
