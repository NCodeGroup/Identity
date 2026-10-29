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
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Claims;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.MutualTls;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.Tests.ProofOfPossession;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Identity.OpenId.Client.UserInfo;
using NCode.Identity.Secrets;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.UserInfo;

public sealed class DefaultUserInfoClientTests : BaseTests, IAsyncLifetime
{
    private const string Authority = "https://op.example.test";
    private const string UserInfoEndpoint = "https://op.example.test/userinfo";
    private const string AccessToken = "access-token";

    private Mock<IProviderMetadataResolver> MockMetadataResolver { get; }
    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private ServiceProvider JwtProvider { get; }
    private DPoPTestContext DPoP { get; } = new();

    public DefaultUserInfoClientTests()
    {
        MockMetadataResolver = CreateStrictMock<IProviderMetadataResolver>();
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        JwtProvider = services.BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await JwtProvider.DisposeAsync();
        await DPoP.DisposeAsync();
    }

    private DefaultUserInfoClient CreateClient(
        StubHttpMessageHandler handler,
        string? userInfoEndpoint = UserInfoEndpoint
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
                        UserInfoEndpoint = userInfoEndpoint,
                        DPoPSigningAlgValuesSupported = ["ES256"],
                    }
                )
            )
            .Verifiable();

        var mockAuthenticator = CreateStrictMock<IClientAuthenticator>();
        mockAuthenticator.Setup(x => x.MethodName).Returns("test").Verifiable();
        if (userInfoEndpoint is not null)
        {
            mockAuthenticator.Setup(x => x.UsesMutualTls).Returns(false).Verifiable();
            MockHttpClientFactory
                .Setup(x => x.CreateClient(OpenIdClientDefaults.HttpClientName))
                .Returns(new HttpClient(handler, disposeHandler: false))
                .Verifiable();
        }

        var providerHttpClient = new ProviderHttpClient(
            MockHttpClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions())
        );
        var sender = new ClientRequestSender(
            providerHttpClient,
            [mockAuthenticator.Object],
            new ClientCertificateProvider(
                CreateStrictMock<IClientKeyRingProvider>().Object,
                TimeProvider.System
            )
        );

        return new DefaultUserInfoClient(
            MockMetadataResolver.Object,
            sender,
            providerHttpClient,
            DPoP.CreateResolver(DPoP.EcKey),
            JwtProvider.GetRequiredService<IClaimsIdentityFactory>()
        );
    }

    private static ClientRegistration CreateRegistration() =>
        new(Authority, "client-1", "test") { DPoPKeyRing = DPoPTestContext.KeyRingName };

    private static HttpResponseMessage CreateChallenge(
        HttpStatusCode statusCode,
        string challenge,
        string? dpopNonce = null
    )
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("", SecureEncoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("WWW-Authenticate", challenge);
        if (dpopNonce is not null)
            response.Headers.Add("DPoP-Nonce", dpopNonce);
        return response;
    }

    #region GetUserInfoAsync Tests

    [Fact]
    public async Task GetUserInfoAsync_WithABearerToken_PresentsItAndMapsTheClaims()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            """{"sub":"user-1","email":"ada@example.com","email_verified":true,"groups":["a","b"]}"""
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(UserInfoEndpoint), request.RequestUri);
        Assert.Equal($"Bearer {AccessToken}", request.Authorization);
        Assert.Contains("application/json", request.Accept);
        Assert.Null(request.DPoP);

        Assert.True(result.IsSuccess);
        Assert.Equal(DefaultUserInfoClient.AuthenticationType, result.Identity.AuthenticationType);
        var email = Assert.Single(result.Identity.FindAll("email"));
        Assert.Equal("ada@example.com", email.Value);
        Assert.Equal(Authority, email.Issuer);
        Assert.Equal(
            ClaimValueTypes.Boolean,
            result.Identity.FindFirst("email_verified")?.ValueType
        );
        Assert.Equal(["a", "b"], result.Identity.FindAll("groups").Select(claim => claim.Value));
        Assert.Equal("user-1", result.Claims.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task GetUserInfoAsync_WithADPoPBoundToken_PresentsItWithAProofOfItsKey()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"sub":"user-1"}""");
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1")
            {
                DPoPJwkThumbprint = DPoP.Thumbprint(DPoP.EcKey),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"DPoP {AccessToken}", request.Authorization);
        Assert.NotNull(request.DPoP);
        var payload = DPoPTestContext.DecodePayload(request.DPoP);
        Assert.Equal("GET", payload.GetProperty("htm").GetString());
        Assert.Equal(UserInfoEndpoint, payload.GetProperty("htu").GetString());
        Assert.True(payload.TryGetProperty("ath", out _));
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenTheEndpointRequiresADPoPNonce_RetriesOnceWithIt()
    {
        var handler = StubHttpMessageHandler.Sequence(
            CreateChallenge(
                HttpStatusCode.Unauthorized,
                "DPoP error=\"use_dpop_nonce\", error_description=\"Resource server requires nonce in DPoP proof\"",
                dpopNonce: "nonce-1"
            ),
            StubHttpMessageHandler.CreateResponse(HttpStatusCode.OK, """{"sub":"user-1"}""")
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1")
            {
                DPoPJwkThumbprint = DPoP.Thumbprint(DPoP.EcKey),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
        var retried = DPoPTestContext.DecodePayload(handler.Requests[1].DPoP!);
        Assert.Equal("nonce-1", retried.GetProperty("nonce").GetString());
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenTheTokenIsRejected_ReturnsTheChallengeError()
    {
        var handler = StubHttpMessageHandler.Sequence(
            CreateChallenge(
                HttpStatusCode.Unauthorized,
                "Bearer realm=\"op\", error=\"invalid_token\", error_description=\"The access token expired\""
            )
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("invalid_token", result.Error.Error);
        Assert.Equal("The access token expired", result.Error.ErrorDescription);
        Assert.Equal(401, result.Error.StatusCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenTheResponseIsAboutAnotherSubject_FailsWithSubjectMismatch()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            """{"sub":"someone-else","email":"eve@example.com"}"""
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(UserInfoFailureCodes.SubjectMismatch, result.FailureCode);
        Assert.Null(result.Identity);
    }

    [Theory]
    [InlineData("""{"email":"ada@example.com"}""")]
    [InlineData("""{"sub":42}""")]
    [InlineData("""["user-1"]""")]
    public async Task GetUserInfoAsync_WhenTheResponseHasNoStringSubject_FailsAsInvalid(string body)
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(UserInfoFailureCodes.InvalidResponse, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenTheResponseIsAJwt_FailsAsUnsupported()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            "eyJhbGciOiJQUzI1NiJ9.eyJzdWIiOiJ1c2VyLTEifQ.c2ln",
            "application/jwt"
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(UserInfoFailureCodes.UnsupportedResponse, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenTheEndpointFailsWithoutAnError_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.InternalServerError, "");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await client.GetUserInfoAsync(
                CreateRegistration(),
                new UserInfoRequest(AccessToken, "user-1"),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenTheProviderHasNoUserInfoEndpoint_Throws()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, userInfoEndpoint: null);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.GetUserInfoAsync(
                CreateRegistration(),
                new UserInfoRequest(AccessToken, "user-1"),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    #endregion
}
