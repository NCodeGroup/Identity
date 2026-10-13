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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultCibaNotificationServiceTests : BaseTests
{
    private const string AuthReqId = "auth-req-id";
    private const string NotificationToken = "notify-token";
    private const string Endpoint = "https://client.test/ciba";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private RecordingHttpMessageHandler Handler { get; }
    private DefaultCibaNotificationService Service { get; }

    public DefaultCibaNotificationServiceTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        Handler = new RecordingHttpMessageHandler();
        Service = new DefaultCibaNotificationService(
            MockHttpClientFactory.Object,
            NullLogger<DefaultCibaNotificationService>.Instance
        );
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public string? Authorization { get; private set; }
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private Mock<OpenIdClient> CreateClient(string? endpoint)
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();

        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint))
            .Returns(endpoint ?? string.Empty)
            .Verifiable();

        return mockClient;
    }

    private void SetupHttpClient() =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(Handler))
            .Verifiable();

    private static CibaGrant CreateGrant(string deliveryMode) =>
        new()
        {
            ClientId = "client-1",
            Scopes = ["openid", "api"],
            Status = DeviceAuthorizationStatus.Approved,
            DeliveryMode = deliveryMode,
            ClientNotificationToken = NotificationToken,
        };

    [Fact]
    public async Task NotifyClientAsync_WhenEndpointConfigured_PostsAuthReqIdWithBearerToken()
    {
        SetupHttpClient();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateClient(Endpoint);

        await Service.NotifyClientAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateGrant(OpenIdConstants.BackchannelTokenDeliveryModes.Ping),
            CancellationToken.None
        );

        Assert.Equal(new Uri(Endpoint), Handler.RequestUri);
        Assert.Equal($"Bearer {NotificationToken}", Handler.Authorization);

        using var document = JsonDocument.Parse(Handler.RequestBody!);
        Assert.Equal(
            AuthReqId,
            document.RootElement.GetProperty(OpenIdConstants.Parameters.AuthReqId).GetString()
        );
    }

    [Fact]
    public async Task NotifyClientAsync_WhenEndpointMissing_DoesNotPost()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateClient(endpoint: null);

        await Service.NotifyClientAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateGrant(OpenIdConstants.BackchannelTokenDeliveryModes.Ping),
            CancellationToken.None
        );

        Assert.Equal(0, Handler.CallCount);
    }

    [Fact]
    public async Task DeliverTokensAsync_WhenEndpointConfigured_PostsTokenResponse()
    {
        SetupHttpClient();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateClient(Endpoint);

        var mockTokenResponse = CreateStrictMock<ITokenResponse>();
        mockTokenResponse.SetupGet(x => x.AccessToken).Returns("access-token-value");
        mockTokenResponse.SetupGet(x => x.TokenType).Returns(OpenIdConstants.TokenTypes.Bearer);
        mockTokenResponse.SetupGet(x => x.ExpiresIn).Returns(TimeSpan.FromHours(1));
        mockTokenResponse.SetupGet(x => x.RefreshToken).Returns((string?)null);
        mockTokenResponse.SetupGet(x => x.IdToken).Returns("id-token-value");
        mockTokenResponse.SetupGet(x => x.Scopes).Returns(["openid", "api"]);

        await Service.DeliverTokensAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateGrant(OpenIdConstants.BackchannelTokenDeliveryModes.Push),
            mockTokenResponse.Object,
            CancellationToken.None
        );

        Assert.Equal(new Uri(Endpoint), Handler.RequestUri);
        Assert.Equal($"Bearer {NotificationToken}", Handler.Authorization);

        using var document = JsonDocument.Parse(Handler.RequestBody!);
        var root = document.RootElement;
        Assert.Equal(AuthReqId, root.GetProperty(OpenIdConstants.Parameters.AuthReqId).GetString());
        Assert.Equal(
            "access-token-value",
            root.GetProperty(OpenIdConstants.Parameters.AccessToken).GetString()
        );
        Assert.Equal(
            OpenIdConstants.TokenTypes.Bearer,
            root.GetProperty(OpenIdConstants.Parameters.TokenType).GetString()
        );
        Assert.Equal(3600, root.GetProperty(OpenIdConstants.Parameters.ExpiresIn).GetInt64());
        Assert.Equal(
            "id-token-value",
            root.GetProperty(OpenIdConstants.Parameters.IdToken).GetString()
        );
        Assert.Equal("openid api", root.GetProperty(OpenIdConstants.Parameters.Scope).GetString());
        Assert.False(root.TryGetProperty(OpenIdConstants.Parameters.RefreshToken, out _));
    }

    [Fact]
    public async Task NotifyAuthenticationDeviceAsync_Always_IsNoOp()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();

        await Service.NotifyAuthenticationDeviceAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateGrant(OpenIdConstants.BackchannelTokenDeliveryModes.Push),
            CancellationToken.None
        );

        Assert.Equal(0, Handler.CallCount);
    }
}
