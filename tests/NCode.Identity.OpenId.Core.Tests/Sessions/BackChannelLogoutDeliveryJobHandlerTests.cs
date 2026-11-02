// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Sessions;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class BackChannelLogoutDeliveryJobHandlerTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string LogoutUri = "https://rp.example/backchannel-logout";
    private const string LogoutToken = "header.payload.signature";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private RecordingHttpMessageHandler Handler { get; }
    private BackChannelLogoutDeliveryJobHandler Subject { get; }

    public BackChannelLogoutDeliveryJobHandlerTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        Handler = new RecordingHttpMessageHandler();
        Subject = new BackChannelLogoutDeliveryJobHandler(MockHttpClientFactory.Object);
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

    private static DispatchContext Context() =>
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    private static BackChannelLogoutDeliveryJob CreateJob() =>
        new()
        {
            ClientId = ClientId,
            LogoutUri = LogoutUri,
            LogoutToken = LogoutToken,
        };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenConfigured_PostsFormEncodedLogoutToken()
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(BackChannelLogoutDeliveryJobHandler.HttpClientName))
            .Returns(new HttpClient(Handler))
            .Verifiable();

        await Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None);

        Assert.Equal(new Uri(LogoutUri), Handler.RequestUri);
        Assert.NotNull(Handler.RequestBody);
        Assert.Contains(OpenIdProtocol.Parameters.LogoutToken, Handler.RequestBody);
        Assert.Contains(LogoutToken, Handler.RequestBody);
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointReturnsError_Throws()
    {
        var errorHandler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        MockHttpClientFactory
            .Setup(x => x.CreateClient(BackChannelLogoutDeliveryJobHandler.HttpClientName))
            .Returns(new HttpClient(errorHandler))
            .Verifiable();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None).AsTask()
        );
    }

    #endregion
}
