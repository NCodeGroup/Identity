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
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(Handler))
            .Verifiable();

        await Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None);

        Assert.Equal(new Uri(LogoutUri), Handler.RequestUri);
        Assert.NotNull(Handler.RequestBody);
        Assert.Contains(OpenIdConstants.Parameters.LogoutToken, Handler.RequestBody);
        Assert.Contains(LogoutToken, Handler.RequestBody);
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointReturnsError_Throws()
    {
        var errorHandler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        MockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(errorHandler))
            .Verifiable();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None).AsTask()
        );
    }

    #endregion
}
