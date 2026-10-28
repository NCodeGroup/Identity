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
using System.Text.Json;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class CibaDeliveryJobHandlerTests : BaseTests
{
    private const string AuthReqId = "auth-req-id";
    private const string NotificationToken = "notify-token";
    private const string Endpoint = "https://client.test/ciba";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private RecordingHttpMessageHandler Handler { get; }
    private CibaDeliveryJobHandler Subject { get; }

    public CibaDeliveryJobHandlerTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        Handler = new RecordingHttpMessageHandler();
        Subject = new CibaDeliveryJobHandler(MockHttpClientFactory.Object);
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

    private void SetupHttpClient() =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(CibaDeliveryJobHandler.HttpClientName))
            .Returns(new HttpClient(Handler))
            .Verifiable();

    private static DispatchContext Context() =>
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    #region HandleAsync Tests

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

        using var document = JsonDocument.Parse(Handler.RequestBody!);
        var root = document.RootElement;
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

        using var document = JsonDocument.Parse(Handler.RequestBody!);
        var root = document.RootElement;
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

        using var document = JsonDocument.Parse(Handler.RequestBody!);
        var root = document.RootElement;
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

    #endregion
}
