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
using NCode.Identity.OpenId.Client.Tests.ProofOfPossession;
using NCode.Identity.OpenId.Client.Tokens;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class DefaultTokenClientTests : BaseTests, IAsyncLifetime
{
    private const string Authority = "https://op.example.test";
    private const string SuccessBody =
        """{"access_token":"at","token_type":"Bearer","expires_in":300,"refresh_token":"rt","id_token":"it","scope":"api"}""";

    private DPoPTestContext DPoPContext { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await DPoPContext.DisposeAsync();

    private Mock<IProviderMetadataResolver> MockMetadataResolver { get; }

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    public DefaultTokenClientTests()
    {
        MockMetadataResolver = CreateStrictMock<IProviderMetadataResolver>();
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private DefaultTokenClient CreateClient(StubHttpMessageHandler handler)
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
                    }
                )
            )
            .Verifiable();

        MockHttpClientFactory
            .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
            .Returns(new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        var mockAuthenticator = CreateStrictMock<IClientAuthenticator>();
        mockAuthenticator.Setup(x => x.MethodName).Returns("test").Verifiable();
        mockAuthenticator
            .Setup(x =>
                x.AuthenticateAsync(It.IsAny<ClientAuthenticationContext>(), CancellationToken.None)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var sender = new ClientRequestSender(
            new ProviderHttpClient(
                MockHttpClientFactory.Object,
                Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions())
            ),
            [mockAuthenticator.Object]
        );

        return new DefaultTokenClient(
            MockMetadataResolver.Object,
            sender,
            DPoPContext.CreateResolver()
        );
    }

    private static ClientRegistration CreateRegistration() => new(Authority, "client-1", "test");

    private static List<KeyValuePair<string, string>> ParseBody(string? body)
    {
        var collection = HttpUtility.ParseQueryString(body ?? string.Empty);
        return collection
            .AllKeys.SelectMany(key =>
                collection
                    .GetValues(key)!
                    .Select(value => new KeyValuePair<string, string>(key!, value))
            )
            .ToList();
    }

    private static List<string> Values(
        List<KeyValuePair<string, string>> parameters,
        string name
    ) => parameters.Where(x => x.Key == name).Select(x => x.Value).ToList();

    #region RequestTokenAsync Tests

    [Fact]
    public async Task RequestTokenAsync_WithAuthorizationCode_PostsParametersAndMapsResponse()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, SuccessBody);
        var client = CreateClient(handler);

        var result = await client.RequestTokenAsync(
            CreateRegistration(),
            new AuthorizationCodeTokenRequest("the-code", "https://app.example.test/cb", "verifier")
            {
                Scope = "api",
                Resources = ["https://r1.example.test", "https://r2.example.test"],
            },
            CancellationToken.None
        );

        var parameters = ParseBody(Assert.Single(handler.Requests).Body);
        Assert.Equal(["authorization_code"], Values(parameters, "grant_type"));
        Assert.Equal(["the-code"], Values(parameters, "code"));
        Assert.Equal(["https://app.example.test/cb"], Values(parameters, "redirect_uri"));
        Assert.Equal(["verifier"], Values(parameters, "code_verifier"));
        Assert.Equal(["api"], Values(parameters, "scope"));
        Assert.Equal(
            ["https://r1.example.test", "https://r2.example.test"],
            Values(parameters, "resource")
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("at", result.Response.AccessToken);
        Assert.Equal("Bearer", result.Response.TokenType);
        Assert.Equal(300, result.Response.ExpiresIn);
        Assert.Equal("rt", result.Response.RefreshToken);
        Assert.Equal("it", result.Response.IdToken);
        Assert.Equal("api", result.Response.Scope);
    }

    [Fact]
    public async Task RequestTokenAsync_WithUndeserializableResponse_ThrowsWithJsonException()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"access_token":123}""");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.RequestTokenAsync(
                CreateRegistration(),
                new RefreshTokenRequest("old-rt"),
                CancellationToken.None
            )
        );

        Assert.IsAssignableFrom<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task RequestTokenAsync_WithRefreshToken_PostsRefreshParameters()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, SuccessBody);
        var client = CreateClient(handler);

        await client.RequestTokenAsync(
            CreateRegistration(),
            new RefreshTokenRequest("old-rt"),
            CancellationToken.None
        );

        var parameters = ParseBody(Assert.Single(handler.Requests).Body);
        Assert.Equal(["refresh_token"], Values(parameters, "grant_type"));
        Assert.Equal(["old-rt"], Values(parameters, "refresh_token"));
    }

    [Fact]
    public async Task RequestTokenAsync_WithClientCredentials_PostsOnlyGrantTypeAndScope()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, SuccessBody);
        var client = CreateClient(handler);

        await client.RequestTokenAsync(
            CreateRegistration(),
            new ClientCredentialsTokenRequest { Scope = "api" },
            CancellationToken.None
        );

        var parameters = ParseBody(Assert.Single(handler.Requests).Body);
        Assert.Equal(2, parameters.Count);
        Assert.Equal(["client_credentials"], Values(parameters, "grant_type"));
        Assert.Equal(["api"], Values(parameters, "scope"));
    }

    [Fact]
    public async Task RequestTokenAsync_WithTokenExchange_PostsExchangeParameters()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, SuccessBody);
        var client = CreateClient(handler);

        await client.RequestTokenAsync(
            CreateRegistration(),
            new TokenExchangeTokenRequest(
                "subject",
                OpenIdProtocol.TokenTypeIdentifiers.AccessToken
            )
            {
                RequestedTokenType = OpenIdProtocol.TokenTypeIdentifiers.AccessToken,
                Audiences = ["aud1", "aud2"],
            },
            CancellationToken.None
        );

        var parameters = ParseBody(Assert.Single(handler.Requests).Body);
        Assert.Equal(["subject"], Values(parameters, "subject_token"));
        Assert.Equal(
            [OpenIdProtocol.TokenTypeIdentifiers.AccessToken],
            Values(parameters, "subject_token_type")
        );
        Assert.Equal(
            [OpenIdProtocol.TokenTypeIdentifiers.AccessToken],
            Values(parameters, "requested_token_type")
        );
        Assert.Equal(["aud1", "aud2"], Values(parameters, "audience"));
    }

    [Fact]
    public async Task RequestTokenAsync_WithOAuthError_ReturnsError()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"d"}"""
        );
        var client = CreateClient(handler);

        var result = await client.RequestTokenAsync(
            CreateRegistration(),
            new RefreshTokenRequest("old-rt"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_grant", result.Error.Error);
        Assert.Equal(400, result.Error.StatusCode);
    }

    [Fact]
    public async Task RequestTokenAsync_WithoutAccessToken_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            """{"token_type":"Bearer"}"""
        );
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.RequestTokenAsync(
                CreateRegistration(),
                new RefreshTokenRequest("old-rt"),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RequestTokenAsync_WithHtmlServerError_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.BadGateway,
            "<html>bad gateway</html>",
            "text/html"
        );
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.RequestTokenAsync(
                CreateRegistration(),
                new RefreshTokenRequest("old-rt"),
                CancellationToken.None
            )
        );
    }

    #endregion
}
