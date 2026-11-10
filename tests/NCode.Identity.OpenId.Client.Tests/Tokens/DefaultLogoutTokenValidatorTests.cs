// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
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
    private const string KeyRingName = "client-ring";
    private const string SignatureFailure =
        "The logout token signature could not be verified with the provider's signing keys.";

    private ServiceProvider ServiceProvider { get; }
    private RSA Rsa { get; } = RSA.Create(2048);
    private RSA EncryptionRsa { get; } = RSA.Create(2048);
    private RSA OtherRsa { get; } = RSA.Create(2048);
    private SecretKey RsaKey { get; }
    private SecretKey EncryptionKey { get; }
    private ClientRegistration Registration { get; } =
        new(Issuer, ClientId, OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

    public DefaultLogoutTokenValidatorTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        RsaKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "k1" }, Rsa);
        EncryptionKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(
                new KeyMetadata { KeyId = "enc-1", Use = SecretKeyUses.Encryption },
                EncryptionRsa
            );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        Rsa.Dispose();
        EncryptionRsa.Dispose();
        OtherRsa.Dispose();
    }

    private ISecretKeyCollection CreateKeys(params SecretKey[] keys) =>
        ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create(keys);

    private Mock<IProviderKeySetResolver> CreateKeySetMock()
    {
        var mock = CreateStrictMock<IProviderKeySetResolver>();
        mock.Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKeys(RsaKey))
            .Verifiable();
        return mock;
    }

    private Mock<IClientKeyRingProvider> CreateKeyRingMock(params SecretKey[] keys)
    {
        var mock = CreateStrictMock<IClientKeyRingProvider>();
        mock.Setup(x => x.GetKeyRing(KeyRingName)).Returns(CreateKeys(keys)).Verifiable();
        return mock;
    }

    private Mock<IExpiringKeySet> CreateAcceptingKeySet()
    {
        var mock = CreateStrictMock<IExpiringKeySet>();
        mock.Setup(x => x.ContainsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();
        return mock;
    }

    private ProviderJwtValidator CreateProviderJwtValidator(
        IProviderKeySetResolver keySetResolver,
        IClientKeyRingProvider keyRingProvider
    ) =>
        new(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            keySetResolver,
            keyRingProvider,
            ServiceProvider.GetRequiredService<IJwtValidator>()
        );

    private DefaultLogoutTokenValidator CreateValidator(
        Mock<IProviderKeySetResolver>? keySetMock,
        Mock<IExpiringKeySet>? keySet = null,
        List<string>? advertised = null,
        Mock<IClientKeyRingProvider>? keyRingMock = null
    )
    {
        var mockMetadata = CreateStrictMock<IProviderMetadataResolver>();
        mockMetadata
            .Setup(x => x.GetMetadataAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OpenIdProviderMetadata
                {
                    Issuer = Issuer,
                    IdTokenSigningAlgValuesSupported = advertised,
                }
            )
            .Verifiable();

        keySetMock ??= CreateStrictMock<IProviderKeySetResolver>();
        keyRingMock ??= CreateStrictMock<IClientKeyRingProvider>();
        keySet ??= CreateStrictMock<IExpiringKeySet>();

        return new DefaultLogoutTokenValidator(
            mockMetadata.Object,
            CreateProviderJwtValidator(keySetMock.Object, keyRingMock.Object),
            keySet.Object
        );
    }

    private JoseEncryptionCredentials CreateEncryptionCredentials(
        SecretKey key,
        string keyManagement = "RSA-OAEP-256",
        string contentEncryption = "A128CBC-HS256"
    )
    {
        var algorithms = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        Assert.True(algorithms.TryGetKeyManagementAlgorithm(keyManagement, out var management));
        Assert.True(
            algorithms.TryGetAuthenticatedEncryptionAlgorithm(
                contentEncryption,
                out var authenticatedEncryption
            )
        );

        return new JoseEncryptionCredentials(key, management, authenticatedEncryption);
    }

    private sealed class LogoutTokenOptions
    {
        public string Issuer { get; init; } = DefaultLogoutTokenValidatorTests.Issuer;
        public string Audience { get; init; } = ClientId;
        public TimeSpan? IssuedOffset { get; init; }
        public TimeSpan? Lifetime { get; init; }
        public string TokenType { get; init; } = JoseTokenTypes.LogoutJwt;
        public bool IncludeSub { get; init; } = true;
        public bool IncludeSid { get; init; } = true;
        public bool IncludeJti { get; init; } = true;
        public bool IncludeEvents { get; init; } = true;
        public IReadOnlyDictionary<string, object>? ExtraClaims { get; init; }
        public string Jti { get; init; } = "jti-1";
        public SecretKey? SigningKey { get; init; }
        public JoseEncryptionCredentials? EncryptionCredentials { get; init; }
        public IReadOnlyDictionary<string, object>? ExtraEncryptionHeaderClaims { get; init; }
    }

    private async ValueTask<string> CreateLogoutTokenAsync(
        LogoutTokenOptions options,
        CancellationToken cancellationToken
    )
    {
        var issuedAt = DateTimeOffset.UtcNow + (options.IssuedOffset ?? TimeSpan.Zero);
        var claims = new Dictionary<string, object>();
        if (options.IncludeSub)
            claims["sub"] = "user-1";
        if (options.IncludeSid)
            claims["sid"] = "sid-1";
        if (options.IncludeJti)
            claims["jti"] = options.Jti;
        if (options.IncludeEvents)
            claims["events"] = new Dictionary<string, object>
            {
                [LogoutEvent] = new Dictionary<string, object>(),
            };
        if (options.ExtraClaims is not null)
        {
            foreach (var (name, value) in options.ExtraClaims)
                claims[name] = value;
        }

        Assert.True(
            ServiceProvider
                .GetRequiredService<IAlgorithmCollectionProvider>()
                .Collection.TryGetSignatureAlgorithm("PS256", out var algorithm)
        );

        return await ServiceProvider
            .GetRequiredService<IJwtEncoder>()
            .EncodeAsync(
                new JwtEncodingParameters
                {
                    SigningCredentials = new JoseSigningCredentials(
                        options.SigningKey ?? RsaKey,
                        algorithm
                    ),
                    EnsureTokenLifetime = false,
                    TokenType = options.TokenType,
                    Issuer = options.Issuer,
                    Audience = new StringValues(options.Audience),
                    IssuedAt = issuedAt,
                    NotBefore = issuedAt,
                    Expires =
                        options.ExtraClaims is not null && options.ExtraClaims.ContainsKey("exp")
                            ? null
                            : issuedAt + (options.Lifetime ?? TimeSpan.FromMinutes(2)),
                    ExtraPayloadClaims = claims,
                    EncryptionCredentials = options.EncryptionCredentials,
                    ExtraEncryptionHeaderClaims = options.ExtraEncryptionHeaderClaims,
                },
                cancellationToken
            );
    }

    private static ClientRegistration CreateEncryptingRegistration(
        IReadOnlyList<string>? keyManagementAlgorithms = null,
        IReadOnlyList<string>? contentEncryptionAlgorithms = null,
        string? keyRing = KeyRingName
    ) =>
        new(Issuer, ClientId, OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt)
        {
            IdTokenKeyManagementAlgorithms = keyManagementAlgorithms ?? ["RSA-OAEP-256"],
            IdTokenContentEncryptionAlgorithms = contentEncryptionAlgorithms,
            KeyRing = keyRing,
        };

    private static ClientRegistration CreateRegistration(
        IReadOnlyList<string> idTokenSigningAlgorithms
    ) =>
        new(Issuer, ClientId, OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt)
        {
            IdTokenSigningAlgorithms = idTokenSigningAlgorithms,
        };

    private sealed class ValidateOptions
    {
        public ClientRegistration? Registration { get; init; }
    }

    private async ValueTask<LogoutTokenValidationResult> ValidateAsync(
        DefaultLogoutTokenValidator validator,
        string logoutToken,
        ValidateOptions options,
        CancellationToken cancellationToken
    ) =>
        await validator.ValidateAsync(
            options.Registration ?? Registration,
            new LogoutTokenValidationRequest { LogoutToken = logoutToken },
            cancellationToken
        );

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenValid_ReturnsIdentifiers()
    {
        var validator = CreateValidator(CreateKeySetMock(), CreateAcceptingKeySet());
        var expected = DateTimeOffset.UtcNow.AddMinutes(2);

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Null(result.FailureCode);
        Assert.Equal(Issuer, result.Issuer);
        Assert.Equal("jti-1", result.TokenId);
        Assert.Equal("user-1", result.Subject);
        Assert.Equal("sid-1", result.SessionId);
        Assert.InRange((result.ExpiresAt - expected).TotalSeconds, -5, 5);
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptedToTheClient_ReturnsIdentifiers()
    {
        var validator = CreateValidator(
            CreateKeySetMock(),
            CreateAcceptingKeySet(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );
        var logoutToken = await CreateLogoutTokenAsync(
            new LogoutTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Null(result.FailureCode);
        Assert.Equal("jti-1", result.TokenId);
        Assert.Equal("user-1", result.Subject);
        Assert.Equal("sid-1", result.SessionId);
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptionIsRegisteredAndTokenIsOnlySigned_FailsWithInvalidToken()
    {
        var validator = CreateValidator(
            CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal(
            "The logout token is not encrypted, but the client registered ID-token encryption.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptedAndNoEncryptionIsRegistered_FailsWithAlgorithmNotPermitted()
    {
        var validator = CreateValidator(null);
        var logoutToken = await CreateLogoutTokenAsync(
            new LogoutTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptedToAnotherKey_FailsWithDecryptionFailed()
    {
        var otherKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(
                new KeyMetadata { KeyId = "enc-2", Use = SecretKeyUses.Encryption },
                OtherRsa
            );
        var validator = CreateValidator(null, keyRingMock: CreateKeyRingMock(otherKey));
        var logoutToken = await CreateLogoutTokenAsync(
            new LogoutTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.DecryptionFailed, result.FailureCode);
        Assert.Equal(
            "The logout token could not be decrypted with the client's keys.",
            result.FailureReason
        );
        Assert.DoesNotContain(logoutToken, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenEnclosedSignatureIsInvalid_FailsWithInvalidSignature()
    {
        var otherSigningKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "k1" }, OtherRsa);
        var validator = CreateValidator(
            CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );
        var logoutToken = await CreateLogoutTokenAsync(
            new LogoutTokenOptions
            {
                SigningKey = otherSigningKey,
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidSignature, result.FailureCode);
        Assert.Equal(SignatureFailure, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenReplicatedIssuerDiffers_FailsWithInvalidIssuer()
    {
        var validator = CreateValidator(
            CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );
        var logoutToken = await CreateLogoutTokenAsync(
            new LogoutTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
                ExtraEncryptionHeaderClaims = new Dictionary<string, object>
                {
                    ["iss"] = "https://evil.example.test",
                },
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidIssuer, result.FailureCode);
        Assert.Equal(
            "The 'iss' header of the encrypted logout token does not match the issuer.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenReplicatedIssuerMatches_ReturnsIdentifiers()
    {
        var validator = CreateValidator(
            CreateKeySetMock(),
            CreateAcceptingKeySet(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );
        var logoutToken = await CreateLogoutTokenAsync(
            new LogoutTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
                ExtraEncryptionHeaderClaims = new Dictionary<string, object> { ["iss"] = Issuer },
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Null(result.FailureCode);
        Assert.Equal("jti-1", result.TokenId);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { Issuer = "https://evil.example.test" },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidIssuer, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAudienceLacksTheClient_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { Audience = "other-client" },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpired_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IssuedOffset = TimeSpan.FromMinutes(-20) },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.Expired, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuedInTheFuture_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IssuedOffset = TimeSpan.FromHours(1) },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.NotYetValid, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenLifetimeExceedsTheMaximum_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { Lifetime = TimeSpan.FromMinutes(30) },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpIsOutOfRange_FailsWithInvalidLifetime()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions
                {
                    ExtraClaims = new Dictionary<string, object> { ["exp"] = 1e300 },
                },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenTypeIsJwt_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { TokenType = JoseTokenTypes.Jwt },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidTokenType, result.FailureCode);
        Assert.Equal(
            "The 'typ' header of the logout token is not logout+jwt.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenUntyped_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock(), CreateAcceptingKeySet());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { TokenType = string.Empty },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderAdvertisesOnlySymmetricAlgorithm_Fails()
    {
        var validator = CreateValidator(null, advertised: ["HS256"]);

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
        Assert.StartsWith("No logout-token signing algorithm", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenRegistrationPermitsOnlyAnotherAlgorithm_Fails()
    {
        var validator = CreateValidator(null);

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions { Registration = CreateRegistration(["RS256"]) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureIsTampered_FailsWithoutTokenText()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var token = await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None);
        var segments = token.Split('.');
        var signature = segments[2];
        segments[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        var tampered = string.Join('.', segments);

        var result = await ValidateAsync(
            validator,
            tampered,
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidSignature, result.FailureCode);
        Assert.Equal(SignatureFailure, result.FailureReason);
        Assert.DoesNotContain(tampered, result.FailureReason);
        Assert.DoesNotContain(token, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenEmpty_Fails()
    {
        var validator = new DefaultLogoutTokenValidator(
            CreateStrictMock<IProviderMetadataResolver>().Object,
            CreateProviderJwtValidator(
                CreateStrictMock<IProviderKeySetResolver>().Object,
                CreateStrictMock<IClientKeyRingProvider>().Object
            ),
            CreateStrictMock<IExpiringKeySet>().Object
        );

        var result = await ValidateAsync(
            validator,
            string.Empty,
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal("The logout token is empty.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubAndSidAreMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IncludeSub = false, IncludeSid = false },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal(
            "The logout token has neither a 'sub' nor a 'sid' claim.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenOnlySid_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock(), CreateAcceptingKeySet());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IncludeSub = false },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Null(result.Subject);
        Assert.Equal("sid-1", result.SessionId);
    }

    [Fact]
    public async Task ValidateAsync_WhenOnlySub_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock(), CreateAcceptingKeySet());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IncludeSid = false },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal("user-1", result.Subject);
        Assert.Null(result.SessionId);
    }

    [Fact]
    public async Task ValidateAsync_WhenSessionRequiredAndSidIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var registration = new ClientRegistration(
            Issuer,
            ClientId,
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt
        )
        {
            BackchannelLogoutSessionRequired = true,
        };

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IncludeSid = false },
                CancellationToken.None
            ),
            new ValidateOptions { Registration = registration },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal(
            "The logout token has no 'sid' claim, which the client requires.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenSidIsNotAString_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions
                {
                    ExtraClaims = new Dictionary<string, object> { ["sid"] = 42 },
                },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal(
            "The 'sid' claim of the logout token is not a non-empty string.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenSubIsNotAString_FailsWithInvalidClaim()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions
                {
                    ExtraClaims = new Dictionary<string, object> { ["sub"] = 42 },
                },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal(
            "The 'sub' claim of the logout token is not a non-empty string.",
            result.FailureReason
        );
    }

    private const string EventsFailure =
        "The 'events' claim of the logout token does not contain the back-channel logout event.";

    [Fact]
    public async Task ValidateAsync_WhenEventsIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IncludeEvents = false },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal(EventsFailure, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenEventsLacksTheLogoutEvent_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions
                {
                    ExtraClaims = new Dictionary<string, object>
                    {
                        ["events"] = new Dictionary<string, object>
                        {
                            ["other"] = new Dictionary<string, object>(),
                        },
                    },
                },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal(EventsFailure, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenLogoutEventIsNotAnObject_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions
                {
                    ExtraClaims = new Dictionary<string, object>
                    {
                        ["events"] = new Dictionary<string, object> { [LogoutEvent] = "x" },
                    },
                },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal(EventsFailure, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceIsPresent_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions
                {
                    ExtraClaims = new Dictionary<string, object> { ["nonce"] = "n" },
                },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal("The logout token contains a 'nonce' claim.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenJtiIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(
                new LogoutTokenOptions { IncludeJti = false },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal("The logout token has no 'jti' claim.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenAlreadyRecorded_FailsAsReplayed()
    {
        var keySet = CreateStrictMock<IExpiringKeySet>();
        keySet
            .Setup(x => x.ContainsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        var keySetMock = CreateKeySetMock();
        var validator = CreateValidator(keySetMock, keySet);

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.TokenReplayed, result.FailureCode);
        Assert.Equal("The logout token has already been handled.", result.FailureReason);
    }

    private static void SetupLookups(Mock<IExpiringKeySet> keySet, List<string> lookups) =>
        keySet
            .Setup(x => x.ContainsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => lookups.Add(key))
            .ReturnsAsync(false)
            .Verifiable();

    private static void SetupRecords(
        Mock<IExpiringKeySet> keySet,
        List<(string Key, DateTimeOffset ExpiresWhen)> records,
        bool added = true
    ) =>
        keySet
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, DateTimeOffset, CancellationToken>(
                (key, expiresWhen, _) => records.Add((key, expiresWhen))
            )
            .ReturnsAsync(added)
            .Verifiable();

    private DefaultLogoutTokenValidator CreateRecordingValidator(Mock<IExpiringKeySet> keySet) =>
        new(
            CreateStrictMock<IProviderMetadataResolver>().Object,
            CreateProviderJwtValidator(
                CreateStrictMock<IProviderKeySetResolver>().Object,
                CreateStrictMock<IClientKeyRingProvider>().Object
            ),
            keySet.Object
        );

    [Fact]
    public async Task ValidateAsync_WhenValid_LooksUpTheScopedJtiWithoutRecordingIt()
    {
        var lookups = new List<string>();
        var keySet = CreateStrictMock<IExpiringKeySet>();
        SetupLookups(keySet, lookups);
        keySet
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Verifiable(Times.Never());
        var keySetMock = CreateKeySetMock();
        var validator = CreateValidator(keySetMock, keySet);

        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        var lookup = Assert.Single(lookups);
        Assert.StartsWith("logout_token_jti:", lookup);
        Assert.DoesNotContain("jti-1", lookup);
    }

    [Fact]
    public async Task ValidateAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var mockKeySet = CreateStrictMock<IProviderKeySetResolver>();
        mockKeySet
            .Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException())
            .Verifiable();
        var validator = CreateValidator(mockKeySet);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ValidateAsync(
                validator,
                await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
                new ValidateOptions(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenKeySetResolverThrows_Throws()
    {
        var mockKeySet = CreateStrictMock<IProviderKeySetResolver>();
        mockKeySet
            .Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .Throws(new OpenIdProviderException("jwks unavailable"))
            .Verifiable();
        var validator = CreateValidator(mockKeySet);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await ValidateAsync(
                validator,
                await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
                new ValidateOptions(),
                CancellationToken.None
            )
        );
    }

    #endregion

    #region TryRecordHandledAsync Tests

    [Fact]
    public async Task TryRecordHandledAsync_WhenValid_RecordsTheLookedUpKeyUntilExpiryPlusSkew()
    {
        var lookups = new List<string>();
        var records = new List<(string Key, DateTimeOffset ExpiresWhen)>();
        var keySet = CreateStrictMock<IExpiringKeySet>();
        SetupLookups(keySet, lookups);
        SetupRecords(keySet, records);
        var keySetMock = CreateKeySetMock();
        var validator = CreateValidator(keySetMock, keySet);
        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );
        Assert.True(result.IsValid, result.FailureReason);

        var recorded = await validator.TryRecordHandledAsync(
            Registration,
            result,
            CancellationToken.None
        );

        Assert.True(recorded);
        var record = Assert.Single(records);
        Assert.Equal(lookups[0], record.Key);
        Assert.Equal(result.ExpiresAt + TimeSpan.FromMinutes(5), record.ExpiresWhen);
    }

    [Fact]
    public async Task TryRecordHandledAsync_WhenAlreadyRecorded_ReturnsFalse()
    {
        var lookups = new List<string>();
        var records = new List<(string Key, DateTimeOffset ExpiresWhen)>();
        var keySet = CreateStrictMock<IExpiringKeySet>();
        SetupLookups(keySet, lookups);
        SetupRecords(keySet, records, added: false);
        var keySetMock = CreateKeySetMock();
        var validator = CreateValidator(keySetMock, keySet);
        var result = await ValidateAsync(
            validator,
            await CreateLogoutTokenAsync(new LogoutTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );
        Assert.True(result.IsValid, result.FailureReason);

        var recorded = await validator.TryRecordHandledAsync(
            Registration,
            result,
            CancellationToken.None
        );

        Assert.False(recorded);
    }

    [Fact]
    public async Task TryRecordHandledAsync_WhenJtiOrIssuerDiffers_RecordsADifferentKey()
    {
        var records = new List<(string Key, DateTimeOffset ExpiresWhen)>();
        var keySet = CreateStrictMock<IExpiringKeySet>();
        SetupRecords(keySet, records);
        var validator = CreateRecordingValidator(keySet);
        var payload = JsonElement.Parse("{}");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(2);

        foreach (
            var (issuer, jti) in new[]
            {
                (Issuer, "jti-1"),
                (Issuer, "jti-2"),
                (Issuer, "jti-1"),
                ("https://other.example.test", "jti-1"),
            }
        )
        {
            await validator.TryRecordHandledAsync(
                Registration,
                new LogoutTokenValidationResult(payload, issuer, jti, expiresAt, "user-1", null),
                CancellationToken.None
            );
        }

        Assert.Equal(4, records.Count);
        Assert.NotEqual(records[0].Key, records[1].Key);
        Assert.Equal(records[0].Key, records[2].Key);
        Assert.NotEqual(records[0].Key, records[3].Key);
    }

    [Fact]
    public async Task TryRecordHandledAsync_WhenResultIsInvalid_ThrowsArgumentException()
    {
        var keySet = CreateStrictMock<IExpiringKeySet>();
        var validator = CreateRecordingValidator(keySet);
        var result = new LogoutTokenValidationResult(
            TokenValidationFailureCodes.InvalidToken,
            "invalid"
        );

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await validator.TryRecordHandledAsync(Registration, result, CancellationToken.None)
        );
    }

    #endregion
}
