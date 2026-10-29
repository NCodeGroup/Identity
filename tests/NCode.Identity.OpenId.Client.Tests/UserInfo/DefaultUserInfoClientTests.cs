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
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Claims;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.MutualTls;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.Tests.ProofOfPossession;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Identity.OpenId.Client.UserInfo;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.UserInfo;

public sealed class DefaultUserInfoClientTests : BaseTests, IAsyncLifetime
{
    private const string Authority = "https://op.example.test";
    private const string UserInfoEndpoint = "https://op.example.test/userinfo";
    private const string AccessToken = "access-token";
    private const string ClientId = "client-1";
    private const string KeyRingName = "client-ring";

    private Mock<IProviderMetadataResolver> MockMetadataResolver { get; }
    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private ServiceProvider JwtProvider { get; }
    private DPoPTestContext DPoP { get; } = new();
    private RSA ProviderRsa { get; } = RSA.Create(2048);
    private RSA OtherRsa { get; } = RSA.Create(2048);
    private RSA EncryptionRsa { get; } = RSA.Create(2048);
    private SecretKey ProviderKey { get; }
    private SecretKey OtherKey { get; }
    private SecretKey EncryptionKey { get; }

    public DefaultUserInfoClientTests()
    {
        MockMetadataResolver = CreateStrictMock<IProviderMetadataResolver>();
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        JwtProvider = services.BuildServiceProvider();

        var keyFactory = JwtProvider.GetRequiredService<ISecretKeyFactory>();
        ProviderKey = keyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, ProviderRsa);
        OtherKey = keyFactory.CreateRsa(new KeyMetadata { KeyId = "k1" }, OtherRsa);
        EncryptionKey = keyFactory.CreateRsa(
            new KeyMetadata { KeyId = "enc-1", Use = SecretKeyUses.Encryption },
            EncryptionRsa
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await JwtProvider.DisposeAsync();
        await DPoP.DisposeAsync();
        ProviderRsa.Dispose();
        OtherRsa.Dispose();
        EncryptionRsa.Dispose();
    }

    private DefaultUserInfoClient CreateClient(
        StubHttpMessageHandler handler,
        string? userInfoEndpoint = UserInfoEndpoint,
        Mock<IProviderKeySetResolver>? keySetMock = null,
        Mock<IClientKeyRingProvider>? keyRingMock = null,
        List<string>? advertisedSigningAlgorithms = null,
        bool sends = true
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
                        UserInfoSigningAlgValuesSupported = advertisedSigningAlgorithms,
                    }
                )
            )
            .Verifiable();

        var mockAuthenticator = CreateStrictMock<IClientAuthenticator>();
        mockAuthenticator.Setup(x => x.MethodName).Returns("test").Verifiable();
        if (userInfoEndpoint is not null && sends)
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
            new ProviderJwtValidator(
                JwtProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
                (keySetMock ?? CreateStrictMock<IProviderKeySetResolver>()).Object,
                (keyRingMock ?? CreateStrictMock<IClientKeyRingProvider>()).Object,
                JwtProvider.GetRequiredService<IJwtValidator>()
            ),
            JwtProvider.GetRequiredService<IClaimsIdentityFactory>()
        );
    }

    private static ClientRegistration CreateRegistration() =>
        new(Authority, ClientId, "test") { DPoPKeyRing = DPoPTestContext.KeyRingName };

    private static ClientRegistration CreateSigningRegistration(
        IReadOnlyList<string>? keyManagementAlgorithms = null,
        IReadOnlyList<string>? contentEncryptionAlgorithms = null
    ) =>
        new(Authority, ClientId, "test")
        {
            KeyRing = KeyRingName,
            UserInfoSigningAlgorithms = ["PS256"],
            UserInfoKeyManagementAlgorithms = keyManagementAlgorithms,
            UserInfoContentEncryptionAlgorithms = contentEncryptionAlgorithms,
        };

    private Mock<IProviderKeySetResolver> CreateKeySetMock()
    {
        var mock = CreateStrictMock<IProviderKeySetResolver>();
        mock.Setup(x => x.GetSigningKeysAsync(Authority, It.IsAny<CancellationToken>()))
            .Returns(
                ValueTask.FromResult(
                    JwtProvider
                        .GetRequiredService<ISecretKeyCollectionFactory>()
                        .Create([ProviderKey])
                )
            )
            .Verifiable();
        return mock;
    }

    private Mock<IClientKeyRingProvider> CreateKeyRingMock()
    {
        var mock = CreateStrictMock<IClientKeyRingProvider>();
        mock.Setup(x => x.GetKeyRing(KeyRingName))
            .Returns(
                JwtProvider
                    .GetRequiredService<ISecretKeyCollectionFactory>()
                    .Create([EncryptionKey])
            )
            .Verifiable();
        return mock;
    }

    // A UserInfo response the provider signs as OpenID Connect Core 1.0 §5.3.2 requires: its issuer, an audience naming
    // the client, and the end-user's claims; encrypted to the client when encryption credentials are given.
    private string CreateUserInfoJwt(
        string issuer = Authority,
        string audience = ClientId,
        string subject = "user-1",
        string alg = "PS256",
        string tokenType = JoseTokenTypes.Jwt,
        SecretKey? signingKey = null,
        DateTimeOffset? expires = null,
        bool encrypted = false
    )
    {
        var algorithms = JwtProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;
        Assert.True(algorithms.TryGetSignatureAlgorithm(alg, out var signatureAlgorithm));

        JoseEncryptionCredentials? encryptionCredentials = null;
        if (encrypted)
        {
            Assert.True(
                algorithms.TryGetKeyManagementAlgorithm("RSA-OAEP-256", out var management)
            );
            Assert.True(
                algorithms.TryGetAuthenticatedEncryptionAlgorithm(
                    "A128CBC-HS256",
                    out var contentEncryption
                )
            );
            encryptionCredentials = new JoseEncryptionCredentials(
                EncryptionKey,
                management,
                contentEncryption
            );
        }

        return JwtProvider
            .GetRequiredService<IJwtEncoder>()
            .Encode(
                new JwtEncodingParameters
                {
                    SigningCredentials = new JoseSigningCredentials(
                        signingKey ?? ProviderKey,
                        signatureAlgorithm
                    ),
                    EncryptionCredentials = encryptionCredentials,
                    EnsureTokenLifetime = false,
                    TokenType = tokenType,
                    Issuer = issuer,
                    Audience = new StringValues(audience),
                    Expires = expires,
                    ExtraPayloadClaims = new Dictionary<string, object>
                    {
                        [JoseClaimNames.Payload.Sub] = subject,
                        ["email"] = "ada@example.com",
                    },
                }
            );
    }

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

    [Fact]
    public async Task GetUserInfoAsync_WhenTheResponseNamesAnotherIssuer_FailsWithInvalidIssuer()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            """{"sub":"user-1","iss":"https://evil.example.test","email":"ada@example.com"}"""
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(UserInfoFailureCodes.InvalidIssuer, result.FailureCode);
        Assert.Null(result.Identity);
    }

    [Fact]
    public async Task GetUserInfoAsync_WhenTheResponseNamesTheProvider_Succeeds()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            $$"""{"sub":"user-1","iss":"{{Authority}}","email":"ada@example.com"}"""
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("ada@example.com", Assert.Single(result.Identity.FindAll("email")).Value);
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
    public async Task GetUserInfoAsync_WithoutUserInfoSigning_WhenTheResponseIsAJwt_FailsAsUnsupported()
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

    #region Signed UserInfo Tests

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_AsksForAJwtAndValidatesIt()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(),
            "application/jwt"
        );
        var client = CreateClient(handler, keySetMock: CreateKeySetMock());

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        var request = Assert.Single(handler.Requests);
        Assert.Contains("application/jwt", request.Accept);
        Assert.True(result.IsSuccess, result.FailureReason);
        Assert.Equal(DefaultUserInfoClient.AuthenticationType, result.Identity.AuthenticationType);
        var email = Assert.Single(result.Identity.FindAll("email"));
        Assert.Equal("ada@example.com", email.Value);
        Assert.Equal(Authority, result.Claims.GetProperty("iss").GetString());
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenTheResponseIsJson_FailsAsUnsupported()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"sub":"user-1"}""");
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(UserInfoFailureCodes.UnsupportedResponse, result.FailureCode);
        Assert.Null(result.Identity);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenAnotherIssuerSignedIt_FailsWithInvalidIssuer()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(issuer: "https://attacker.example.test"),
            "application/jwt"
        );
        var client = CreateClient(handler, keySetMock: CreateKeySetMock());

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.InvalidIssuer, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenItNamesAnotherAudience_FailsWithInvalidAudience()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(audience: "client-2"),
            "application/jwt"
        );
        var client = CreateClient(handler, keySetMock: CreateKeySetMock());

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenAnotherKeySignedIt_FailsWithInvalidSignature()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(signingKey: OtherKey),
            "application/jwt"
        );
        var client = CreateClient(handler, keySetMock: CreateKeySetMock());

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.InvalidSignature, result.FailureCode);
        Assert.Equal(
            "The UserInfo response signature could not be verified with the provider's signing keys.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenSignedWithAnUnregisteredAlgorithm_FailsWithAlgorithmNotPermitted()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(alg: "RS256"),
            "application/jwt"
        );
        var client = CreateClient(handler);

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenTheProviderAdvertisesNoneOfItsAlgorithms_FailsWithAlgorithmNotPermitted()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(),
            "application/jwt"
        );
        var client = CreateClient(handler, advertisedSigningAlgorithms: ["ES256"]);

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
        Assert.Equal(
            "No UserInfo signing algorithm is both permitted by the client and advertised by the provider.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenItIsAnotherKindOfToken_FailsWithInvalidTokenType()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(tokenType: "at+jwt"),
            "application/jwt"
        );
        var client = CreateClient(handler, keySetMock: CreateKeySetMock());

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.InvalidTokenType, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenItHasExpired_Fails()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(expires: DateTimeOffset.UtcNow.AddHours(-1)),
            "application/jwt"
        );
        var client = CreateClient(handler, keySetMock: CreateKeySetMock());

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.Expired, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoSigning_WhenItIsAboutAnotherSubject_FailsWithSubjectMismatch()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(subject: "someone-else"),
            "application/jwt"
        );
        var client = CreateClient(handler, keySetMock: CreateKeySetMock());

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(UserInfoFailureCodes.SubjectMismatch, result.FailureCode);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoEncryption_DecryptsAndValidatesTheNestedJwt()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(encrypted: true),
            "application/jwt"
        );
        var client = CreateClient(
            handler,
            keySetMock: CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock()
        );

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(keyManagementAlgorithms: ["RSA-OAEP-256"]),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess, result.FailureReason);
        Assert.Equal("ada@example.com", result.Identity.FindFirst("email")?.Value);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoEncryption_WhenTheResponseIsOnlySigned_FailsAsInvalidToken()
    {
        var handler = StubHttpMessageHandler.Respond(
            HttpStatusCode.OK,
            CreateUserInfoJwt(),
            "application/jwt"
        );
        var client = CreateClient(
            handler,
            keySetMock: CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock()
        );

        var result = await client.GetUserInfoAsync(
            CreateSigningRegistration(keyManagementAlgorithms: ["RSA-OAEP-256"]),
            new UserInfoRequest(AccessToken, "user-1"),
            CancellationToken.None
        );

        Assert.Equal(TokenValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal(
            "The UserInfo response is not encrypted, but the client registered UserInfo encryption.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task GetUserInfoAsync_WithUserInfoEncryptionButNoSigning_ThrowsBeforeSending()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"sub":"user-1"}""");
        var client = CreateClient(handler, sends: false);
        var registration = new ClientRegistration(Authority, ClientId, "test")
        {
            KeyRing = KeyRingName,
            UserInfoKeyManagementAlgorithms = ["RSA-OAEP-256"],
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.GetUserInfoAsync(
                registration,
                new UserInfoRequest(AccessToken, "user-1"),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithContentEncryptionButNoKeyManagement_ThrowsBeforeSending()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, """{"sub":"user-1"}""");
        var client = CreateClient(handler, sends: false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.GetUserInfoAsync(
                CreateSigningRegistration(contentEncryptionAlgorithms: ["A256GCM"]),
                new UserInfoRequest(AccessToken, "user-1"),
                CancellationToken.None
            )
        );

        Assert.Empty(handler.Requests);
    }

    #endregion
}
