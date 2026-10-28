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
using NCode.Identity.OpenId.Client.Authorization;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.Tokens;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Authorization;

public class DefaultAuthorizationRequestClientTests : BaseTests
{
    private const string Authority = "https://op.example.test";

    // RFC 7636 Appendix B
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private Mock<IProviderMetadataResolver> MockMetadataResolver { get; }

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    public DefaultAuthorizationRequestClientTests()
    {
        MockMetadataResolver = CreateStrictMock<IProviderMetadataResolver>();
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private DefaultAuthorizationRequestClient CreateClient(
        StubHttpMessageHandler handler,
        OpenIdProviderMetadata metadata,
        bool expectPush = false,
        Action<ClientAuthenticationContext>? authenticate = null
    )
    {
        MockMetadataResolver
            .Setup(x => x.GetMetadataAsync(Authority, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(metadata))
            .Verifiable();

        var mockAuthenticator = CreateStrictMock<IClientAuthenticator>();
        mockAuthenticator.Setup(x => x.MethodName).Returns("test").Verifiable();

        if (expectPush)
        {
            MockHttpClientFactory
                .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
                .Returns(new HttpClient(handler, disposeHandler: false))
                .Verifiable();

            mockAuthenticator
                .Setup(x =>
                    x.AuthenticateAsync(
                        It.IsAny<ClientAuthenticationContext>(),
                        CancellationToken.None
                    )
                )
                .Callback<ClientAuthenticationContext, CancellationToken>(
                    (c, _) => authenticate?.Invoke(c)
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

        return new DefaultAuthorizationRequestClient(MockMetadataResolver.Object, sender);
    }

    private static OpenIdProviderMetadata CreateMetadata(
        string? authorizationEndpoint = "https://op.example.test/authorize",
        string? parEndpoint = null,
        bool requirePar = false,
        List<string>? challengeMethods = null
    ) =>
        new()
        {
            Issuer = Authority,
            TokenEndpoint = "https://op.example.test/token",
            JwksUri = "https://op.example.test/jwks",
            AuthorizationEndpoint = authorizationEndpoint,
            PushedAuthorizationRequestEndpoint = parEndpoint,
            RequirePushedAuthorizationRequests = requirePar,
            CodeChallengeMethodsSupported = challengeMethods,
        };

    private static ClientRegistration CreateRegistration(bool requirePar = false) =>
        new(Authority, "client-1", "test") { RequirePushedAuthorizationRequests = requirePar };

    private static Uri GetUri(AuthorizationRequestResult result) =>
        Assert.IsType<Uri>(result.AuthorizationUri);

    private static AuthorizationRequest CreateRequest() =>
        new("https://app.example.test/cb", "state-1", Verifier)
        {
            Nonce = "n-1",
            Scope = "openid profile",
            ResponseMode = "query",
            Resources = ["https://api1.example.test", "https://api2.example.test"],
            AdditionalParameters = new Dictionary<string, string> { ["prompt"] = "login" },
        };

    #region CreateAuthorizationUriAsync Tests

    [Fact]
    public async Task CreateAuthorizationUriAsync_WithoutParEndpoint_BuildsFrontChannelUri()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler, CreateMetadata());

        var result = await client.CreateAuthorizationUriAsync(
            CreateRegistration(),
            CreateRequest(),
            CancellationToken.None
        );

        Assert.Empty(handler.Requests);
        Assert.False(result.IsPushed);
        Assert.Equal("/authorize", GetUri(result).AbsolutePath);
        var query = HttpUtility.ParseQueryString(GetUri(result).Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("client-1", query["client_id"]);
        Assert.Equal("https://app.example.test/cb", query["redirect_uri"]);
        Assert.Equal("state-1", query["state"]);
        Assert.Equal(Challenge, query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("n-1", query["nonce"]);
        Assert.Equal("openid profile", query["scope"]);
        Assert.Equal("query", query["response_mode"]);
        Assert.Equal(
            ["https://api1.example.test", "https://api2.example.test"],
            Assert.IsType<string[]>(query.GetValues("resource"))
        );
        Assert.Equal("login", query["prompt"]);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WithEndpointQuery_KeepsExistingQuery()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(
            handler,
            CreateMetadata(authorizationEndpoint: "https://op.example.test/authorize?tenant=a")
        );

        var result = await client.CreateAuthorizationUriAsync(
            CreateRegistration(),
            CreateRequest(),
            CancellationToken.None
        );

        var query = HttpUtility.ParseQueryString(GetUri(result).Query);
        Assert.Equal("a", query["tenant"]);
        Assert.Equal("client-1", query["client_id"]);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WhenProviderRequiresParWithoutEndpoint_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler, CreateMetadata(requirePar: true));

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.CreateAuthorizationUriAsync(
                CreateRegistration(),
                CreateRequest(),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WhenRegistrationRequiresParWithoutEndpoint_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler, CreateMetadata());

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.CreateAuthorizationUriAsync(
                CreateRegistration(requirePar: true),
                CreateRequest(),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WithoutAuthorizationEndpoint_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler, CreateMetadata(authorizationEndpoint: null));

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.CreateAuthorizationUriAsync(
                CreateRegistration(),
                CreateRequest(),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WithInsecureAuthorizationEndpoint_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(
            handler,
            CreateMetadata(authorizationEndpoint: "http://op.example.test/authorize")
        );

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.CreateAuthorizationUriAsync(
                CreateRegistration(),
                CreateRequest(),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WhenProviderLacksS256_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler, CreateMetadata(challengeMethods: ["plain"]));

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.CreateAuthorizationUriAsync(
                CreateRegistration(),
                CreateRequest(),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WhenProviderAdvertisesS256_BuildsUri()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "");
        var client = CreateClient(handler, CreateMetadata(challengeMethods: ["plain", "S256"]));

        var result = await client.CreateAuthorizationUriAsync(
            CreateRegistration(),
            CreateRequest(),
            CancellationToken.None
        );

        Assert.Empty(handler.Requests);
        Assert.False(result.IsPushed);
        Assert.Equal("/authorize", GetUri(result).AbsolutePath);
    }

    #endregion
}
