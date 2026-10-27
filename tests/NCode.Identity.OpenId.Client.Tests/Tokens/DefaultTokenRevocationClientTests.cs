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
using System.Web;
using Moq;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.Tokens;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class DefaultTokenRevocationClientTests : BaseTests
{
    private const string Authority = "https://op.example.test";

    private Mock<IProviderMetadataResolver> MockMetadataResolver { get; }

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    public DefaultTokenRevocationClientTests()
    {
        MockMetadataResolver = CreateStrictMock<IProviderMetadataResolver>();
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private DefaultTokenRevocationClient CreateClient(
        StubHttpMessageHandler handler,
        string? revocationEndpoint = "https://op.example.test/revoke"
    )
    {
        MockMetadataResolver
            .Setup(x => x.GetMetadataAsync(Authority, It.IsAny<CancellationToken>()))
            .Returns(
                ValueTask.FromResult(
                    new OpenIdProviderMetadata
                    {
                        Issuer = Authority,
                        TokenEndpoint = "https://op.example.test/token",
                        JwksUri = "https://op.example.test/jwks",
                        RevocationEndpoint = revocationEndpoint,
                    }
                )
            )
            .Verifiable();

        if (revocationEndpoint is not null)
        {
            MockHttpClientFactory
                .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
                .Returns(new HttpClient(handler, disposeHandler: false))
                .Verifiable();
        }

        var mockAuthenticator = CreateStrictMock<IClientAuthenticator>();
        mockAuthenticator.Setup(x => x.MethodName).Returns("test").Verifiable();

        if (revocationEndpoint is not null)
        {
            mockAuthenticator
                .Setup(x =>
                    x.AuthenticateAsync(
                        It.IsAny<ClientAuthenticationContext>(),
                        CancellationToken.None
                    )
                )
                .Returns(ValueTask.CompletedTask)
                .Verifiable();
        }

        var sender = new ClientRequestSender(
            new ProviderHttpClient(
                MockHttpClientFactory.Object,
                Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions())
            ),
            [mockAuthenticator.Object]
        );

        return new DefaultTokenRevocationClient(MockMetadataResolver.Object, sender);
    }

    private static ClientRegistration CreateRegistration() => new(Authority, "client-1", "test");

    #region RevokeTokenAsync Tests

    [Fact]
    public async Task RevokeTokenAsync_WithEmptySuccessBody_ReturnsNullAndPostsParameters()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler);

        var error = await client.RevokeTokenAsync(
            CreateRegistration(),
            new TokenRevocationRequest("t") { TokenTypeHint = "refresh_token" },
            CancellationToken.None
        );

        Assert.Null(error);
        var collection = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Body ?? "");
        Assert.Equal("t", collection["token"]);
        Assert.Equal("refresh_token", collection["token_type_hint"]);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithOAuthError_ReturnsError()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.BadRequest,
            """{"error":"unsupported_token_type"}"""
        );
        var client = CreateClient(handler);

        var error = await client.RevokeTokenAsync(
            CreateRegistration(),
            new TokenRevocationRequest("t"),
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal("unsupported_token_type", error.Error);
        Assert.Equal((int)HttpStatusCode.BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithServerErrorWithoutOAuthError_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.InternalServerError,
            "oops",
            "text/plain"
        );
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.RevokeTokenAsync(
                CreateRegistration(),
                new TokenRevocationRequest("t"),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RevokeTokenAsync_WithoutRevocationEndpoint_ThrowsAndSendsNothing()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler, revocationEndpoint: null);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.RevokeTokenAsync(
                CreateRegistration(),
                new TokenRevocationRequest("t"),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    #endregion
}
