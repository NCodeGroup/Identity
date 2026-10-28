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

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class DefaultLogoutTokenValidatorTests : BaseTests, IAsyncLifetime
{
    private const string Issuer = "https://op.example.test";
    private const string ClientId = "client-1";
    private const string LogoutEvent = "http://schemas.openid.net/event/backchannel-logout";
    private const string SignatureFailure =
        "The logout token signature could not be verified with the provider's signing keys.";

    private ServiceProvider ServiceProvider { get; }
    private RSA Rsa { get; } = RSA.Create(2048);
    private SecretKey RsaKey { get; }
    private ClientRegistration Registration { get; } =
        new(Issuer, ClientId, ClientAuthenticationMethods.PrivateKeyJwt);

    public DefaultLogoutTokenValidatorTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        RsaKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "k1" }, Rsa);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        Rsa.Dispose();
    }

    private ISecretKeyCollection CreateKeys(params SecretKey[] keys) =>
        ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create(keys);

    private Mock<IProviderKeySetResolver> CreateKeySetMock()
    {
        var mock = CreateStrictMock<IProviderKeySetResolver>();
        mock.Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(CreateKeys(RsaKey)))
            .Verifiable();
        return mock;
    }

    private Mock<IExpiringKeySet> CreateAcceptingKeySet()
    {
        var mock = CreateStrictMock<IExpiringKeySet>();
        mock.Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        return mock;
    }

    private DefaultLogoutTokenValidator CreateValidator(
        Mock<IProviderKeySetResolver>? keySetMock,
        Mock<IExpiringKeySet>? keySet = null,
        List<string>? advertised = null
    )
    {
        var mockMetadata = CreateStrictMock<IProviderMetadataResolver>();
        mockMetadata
            .Setup(x => x.GetMetadataAsync(Issuer, It.IsAny<CancellationToken>()))
            .Returns(
                ValueTask.FromResult(
                    new OpenIdProviderMetadata
                    {
                        Issuer = Issuer,
                        IdTokenSigningAlgValuesSupported = advertised,
                    }
                )
            )
            .Verifiable();

        keySetMock ??= CreateStrictMock<IProviderKeySetResolver>();
        keySet ??= CreateStrictMock<IExpiringKeySet>();

        return new DefaultLogoutTokenValidator(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            mockMetadata.Object,
            keySetMock.Object,
            ServiceProvider.GetRequiredService<IJwtValidator>(),
            keySet.Object
        );
    }

    private string CreateLogoutToken(
        string issuer = Issuer,
        string audience = ClientId,
        TimeSpan? issuedOffset = null,
        TimeSpan? lifetime = null,
        string tokenType = JoseTokenTypes.LogoutJwt,
        bool includeSub = true,
        bool includeSid = true,
        bool includeJti = true,
        bool includeEvents = true,
        IReadOnlyDictionary<string, object>? extraClaims = null,
        string jti = "jti-1"
    )
    {
        var issuedAt = DateTimeOffset.UtcNow + (issuedOffset ?? TimeSpan.Zero);
        var claims = new Dictionary<string, object>();
        if (includeSub)
            claims["sub"] = "user-1";
        if (includeSid)
            claims["sid"] = "sid-1";
        if (includeJti)
            claims["jti"] = jti;
        if (includeEvents)
            claims["events"] = new Dictionary<string, object>
            {
                [LogoutEvent] = new Dictionary<string, object>(),
            };
        if (extraClaims is not null)
        {
            foreach (var (name, value) in extraClaims)
                claims[name] = value;
        }

        Assert.True(
            ServiceProvider
                .GetRequiredService<IAlgorithmCollectionProvider>()
                .Collection.TryGetSignatureAlgorithm("PS256", out var algorithm)
        );

        return ServiceProvider
            .GetRequiredService<IJwtEncoder>()
            .Encode(
                new JwtEncodingParameters
                {
                    SigningCredentials = new JoseSigningCredentials(RsaKey, algorithm),
                    EnsureTokenLifetime = false,
                    TokenType = tokenType,
                    Issuer = issuer,
                    Audience = new StringValues(audience),
                    IssuedAt = issuedAt,
                    NotBefore = issuedAt,
                    Expires = issuedAt + (lifetime ?? TimeSpan.FromMinutes(2)),
                    ExtraPayloadClaims = claims,
                }
            );
    }

    private static ClientRegistration CreateRegistration(
        IReadOnlyList<string> idTokenSigningAlgorithms
    ) =>
        new(Issuer, ClientId, ClientAuthenticationMethods.PrivateKeyJwt)
        {
            IdTokenSigningAlgorithms = idTokenSigningAlgorithms,
        };

    private async ValueTask<LogoutTokenValidationResult> ValidateAsync(
        DefaultLogoutTokenValidator validator,
        string logoutToken,
        ClientRegistration? registration = null
    ) =>
        await validator.ValidateAsync(
            registration ?? Registration,
            new LogoutTokenValidationRequest { LogoutToken = logoutToken },
            CancellationToken.None
        );

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenValid_ReturnsIdentifiers()
    {
        var validator = CreateValidator(CreateKeySetMock(), CreateAcceptingKeySet());
        var expected = DateTimeOffset.UtcNow.AddMinutes(2);

        var result = await ValidateAsync(validator, CreateLogoutToken());

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal("jti-1", result.TokenId);
        Assert.Equal("user-1", result.Subject);
        Assert.Equal("sid-1", result.SessionId);
        Assert.InRange((result.ExpiresAt - expected).TotalSeconds, -5, 5);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateLogoutToken(issuer: "https://evil.example.test")
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAudienceLacksTheClient_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateLogoutToken(audience: "other-client"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpired_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateLogoutToken(issuedOffset: TimeSpan.FromMinutes(-20))
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuedInTheFuture_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateLogoutToken(issuedOffset: TimeSpan.FromHours(1))
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenLifetimeExceedsTheMaximum_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateLogoutToken(lifetime: TimeSpan.FromMinutes(30))
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenTypeIsJwt_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateLogoutToken(tokenType: JoseTokenTypes.Jwt)
        );

        Assert.False(result.IsValid);
        Assert.Equal(
            "The 'typ' header of the logout token is not logout+jwt.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenUntyped_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock(), CreateAcceptingKeySet());

        var result = await ValidateAsync(validator, CreateLogoutToken(tokenType: string.Empty));

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderAdvertisesOnlySymmetricAlgorithm_Fails()
    {
        var validator = CreateValidator(null, advertised: ["HS256"]);

        var result = await ValidateAsync(validator, CreateLogoutToken());

        Assert.False(result.IsValid);
        Assert.StartsWith("No logout-token signing algorithm", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenRegistrationPermitsOnlyAnotherAlgorithm_Fails()
    {
        var validator = CreateValidator(null);

        var result = await ValidateAsync(
            validator,
            CreateLogoutToken(),
            CreateRegistration(["RS256"])
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureIsTampered_FailsWithoutTokenText()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var token = CreateLogoutToken();
        var segments = token.Split('.');
        var signature = segments[2];
        segments[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        var tampered = string.Join('.', segments);

        var result = await ValidateAsync(validator, tampered);

        Assert.False(result.IsValid);
        Assert.Equal(SignatureFailure, result.FailureReason);
        Assert.DoesNotContain(tampered, result.FailureReason);
        Assert.DoesNotContain(token, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenEmpty_Fails()
    {
        var validator = new DefaultLogoutTokenValidator(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            CreateStrictMock<IProviderMetadataResolver>().Object,
            CreateStrictMock<IProviderKeySetResolver>().Object,
            ServiceProvider.GetRequiredService<IJwtValidator>(),
            CreateStrictMock<IExpiringKeySet>().Object
        );

        var result = await ValidateAsync(validator, string.Empty);

        Assert.False(result.IsValid);
        Assert.Equal("The logout token is empty.", result.FailureReason);
    }

    #endregion
}
