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
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.Tokens;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class ClientRequestSenderTests : BaseTests
{
    private const string Endpoint = "https://op.example.test/token";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    public ClientRequestSenderTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private Mock<IClientAuthenticator> CreateAuthenticatorMock()
    {
        var mock = CreateStrictMock<IClientAuthenticator>();
        mock.Setup(x => x.MethodName).Returns("test").Verifiable();
        return mock;
    }

    private ClientRequestSender CreateSender(params IClientAuthenticator[] authenticators) =>
        new(
            new ProviderHttpClient(
                MockHttpClientFactory.Object,
                Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions())
            ),
            authenticators
        );

    private void SetupHttpClient(HttpMessageHandler handler) =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
            .Returns(new HttpClient(handler, disposeHandler: false))
            .Verifiable();

    private static ClientRegistration CreateRegistration(string method = "test") =>
        new("https://op.example.test", "client-1", method);

    #region PostFormAsync Tests

    [Fact]
    public async Task PostFormAsync_WithKnownMethod_PostsAuthenticatedForm()
    {
        var mockAuthenticator = CreateAuthenticatorMock();
        mockAuthenticator
            .Setup(x =>
                x.AuthenticateAsync(It.IsAny<ClientAuthenticationContext>(), CancellationToken.None)
            )
            .Callback<ClientAuthenticationContext, CancellationToken>(
                (context, _) =>
                    context.Parameters.Add(new KeyValuePair<string, string>("auth", "1"))
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        SetupHttpClient(handler);
        var sender = CreateSender(mockAuthenticator.Object);

        var response = await sender.PostFormAsync(
            CreateRegistration(),
            new OpenIdProviderMetadata(),
            Endpoint,
            [new KeyValuePair<string, string>("a", "b")],
            CancellationToken.None
        );

        Assert.Equal(200, response.StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Endpoint, request.RequestUri?.AbsoluteUri);
        Assert.Equal("application/json", request.Accept);
        Assert.Equal("a=b&auth=1", request.Body);
    }

    [Fact]
    public async Task PostFormAsync_WhenIncludeClientIdAndAuthenticatorAddsNone_AppendsClientId()
    {
        var mockAuthenticator = CreateAuthenticatorMock();
        mockAuthenticator
            .Setup(x =>
                x.AuthenticateAsync(It.IsAny<ClientAuthenticationContext>(), CancellationToken.None)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        SetupHttpClient(handler);
        var sender = CreateSender(mockAuthenticator.Object);

        await sender.PostFormAsync(
            CreateRegistration(),
            new OpenIdProviderMetadata(),
            Endpoint,
            [new KeyValuePair<string, string>("a", "b")],
            includeClientId: true,
            CancellationToken.None
        );

        var request = Assert.Single(handler.Requests);
        Assert.Equal("a=b&client_id=client-1", request.Body);
    }

    [Fact]
    public async Task PostFormAsync_WhenIncludeClientIdAndAuthenticatorAddsClientId_SendsItOnce()
    {
        var mockAuthenticator = CreateAuthenticatorMock();
        mockAuthenticator
            .Setup(x =>
                x.AuthenticateAsync(It.IsAny<ClientAuthenticationContext>(), CancellationToken.None)
            )
            .Callback<ClientAuthenticationContext, CancellationToken>(
                (context, _) =>
                    context.Parameters.Add(
                        new KeyValuePair<string, string>("client_id", "client-1")
                    )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        SetupHttpClient(handler);
        var sender = CreateSender(mockAuthenticator.Object);

        await sender.PostFormAsync(
            CreateRegistration(),
            new OpenIdProviderMetadata(),
            Endpoint,
            [new KeyValuePair<string, string>("a", "b")],
            includeClientId: true,
            CancellationToken.None
        );

        var request = Assert.Single(handler.Requests);
        Assert.Equal("a=b&client_id=client-1", request.Body);
    }

    [Fact]
    public async Task PostFormAsync_WhenNotIncludeClientId_SendsNoClientId()
    {
        var mockAuthenticator = CreateAuthenticatorMock();
        mockAuthenticator
            .Setup(x =>
                x.AuthenticateAsync(It.IsAny<ClientAuthenticationContext>(), CancellationToken.None)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        SetupHttpClient(handler);
        var sender = CreateSender(mockAuthenticator.Object);

        await sender.PostFormAsync(
            CreateRegistration(),
            new OpenIdProviderMetadata(),
            Endpoint,
            [new KeyValuePair<string, string>("a", "b")],
            includeClientId: false,
            CancellationToken.None
        );

        var request = Assert.Single(handler.Requests);
        Assert.Equal("a=b", request.Body);
    }

    [Fact]
    public async Task PostFormAsync_WithUnknownMethod_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        var sender = CreateSender(CreateAuthenticatorMock().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sender.PostFormAsync(
                CreateRegistration("other"),
                new OpenIdProviderMetadata(),
                Endpoint,
                [],
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PostFormAsync_WithInsecureEndpoint_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        var sender = CreateSender(CreateAuthenticatorMock().Object);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await sender.PostFormAsync(
                CreateRegistration(),
                new OpenIdProviderMetadata(),
                "http://op.example.test/token",
                [],
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithDuplicateMethodNames_Throws()
    {
        var first = CreateAuthenticatorMock();
        var second = CreateAuthenticatorMock();

        Assert.Throws<InvalidOperationException>(() => CreateSender(first.Object, second.Object));
    }

    #endregion

    #region TryRead Tests

    [Fact]
    public void TryRead_WithErrorObject_ReturnsError()
    {
        var response = new ProviderHttpResponse(
            400,
            JsonDocument.Parse("""{"error":"invalid_grant","error_description":"d"}""").RootElement
        );

        var result = ProtocolErrorReader.TryRead(response, out var error);

        Assert.True(result);
        Assert.NotNull(error);
        Assert.Equal("invalid_grant", error.Error);
        Assert.Equal("d", error.ErrorDescription);
        Assert.Null(error.ErrorUri);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void TryRead_WithEmptyObject_ReturnsFalse()
    {
        var response = new ProviderHttpResponse(400, JsonDocument.Parse("{}").RootElement);

        var result = ProtocolErrorReader.TryRead(response, out var error);

        Assert.False(result);
        Assert.Null(error);
    }

    [Fact]
    public void TryRead_WithNullJson_ReturnsFalse()
    {
        var response = new ProviderHttpResponse(400, null);

        var result = ProtocolErrorReader.TryRead(response, out var error);

        Assert.False(result);
        Assert.Null(error);
    }

    #endregion
}
