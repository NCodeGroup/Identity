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
            .Returns(ValueTask.FromResult(CreateKeys(RsaKey)))
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
            .Returns(ValueTask.FromResult(false))
            .Verifiable();
        return mock;
    }

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
        keyRingMock ??= CreateStrictMock<IClientKeyRingProvider>();
        keySet ??= CreateStrictMock<IExpiringKeySet>();

        return new DefaultLogoutTokenValidator(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            mockMetadata.Object,
            keySetMock.Object,
            keyRingMock.Object,
            ServiceProvider.GetRequiredService<IJwtValidator>(),
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
        string jti = "jti-1",
        SecretKey? signingKey = null,
        JoseEncryptionCredentials? encryptionCredentials = null,
        IReadOnlyDictionary<string, object>? extraEncryptionHeaderClaims = null
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
                    SigningCredentials = new JoseSigningCredentials(
                        signingKey ?? RsaKey,
                        algorithm
                    ),
                    EnsureTokenLifetime = false,
                    TokenType = tokenType,
                    Issuer = issuer,
                    Audience = new StringValues(audience),
                    IssuedAt = issuedAt,
                    NotBefore = issuedAt,
                    Expires =
                        extraClaims is not null && extraClaims.ContainsKey("exp")
                            ? null
                            : issuedAt + (lifetime ?? TimeSpan.FromMinutes(2)),
                    ExtraPayloadClaims = claims,
                    EncryptionCredentials = encryptionCredentials,
                    ExtraEncryptionHeaderClaims = extraEncryptionHeaderClaims,
                }
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
        var logoutToken = CreateLogoutToken(
            encryptionCredentials: CreateEncryptionCredentials(EncryptionKey)
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            registration: CreateEncryptingRegistration()
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
            CreateLogoutToken(),
            registration: CreateEncryptingRegistration()
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
        var logoutToken = CreateLogoutToken(
            encryptionCredentials: CreateEncryptionCredentials(EncryptionKey)
        );

        var result = await ValidateAsync(validator, logoutToken);

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
        var logoutToken = CreateLogoutToken(
            encryptionCredentials: CreateEncryptionCredentials(EncryptionKey)
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            registration: CreateEncryptingRegistration()
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
        var logoutToken = CreateLogoutToken(
            signingKey: otherSigningKey,
            encryptionCredentials: CreateEncryptionCredentials(EncryptionKey)
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            registration: CreateEncryptingRegistration()
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
        var logoutToken = CreateLogoutToken(
            encryptionCredentials: CreateEncryptionCredentials(EncryptionKey),
            extraEncryptionHeaderClaims: new Dictionary<string, object>
            {
                ["iss"] = "https://evil.example.test",
            }
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            registration: CreateEncryptingRegistration()
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
        var logoutToken = CreateLogoutToken(
            encryptionCredentials: CreateEncryptionCredentials(EncryptionKey),
            extraEncryptionHeaderClaims: new Dictionary<string, object> { ["iss"] = Issuer }
        );

        var result = await ValidateAsync(
            validator,
            logoutToken,
            registration: CreateEncryptingRegistration()
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
            CreateLogoutToken(issuer: "https://evil.example.test")
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidIssuer, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAudienceLacksTheClient_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateLogoutToken(audience: "other-client"));

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidAudience, result.FailureCode);
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
        Assert.Equal(TokenValidationFailureCodes.Expired, result.FailureCode);
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
        Assert.Equal(TokenValidationFailureCodes.NotYetValid, result.FailureCode);
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
        Assert.Equal(TokenValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpIsOutOfRange_FailsWithInvalidLifetime()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateLogoutToken(extraClaims: new Dictionary<string, object> { ["exp"] = 1e300 })
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
            CreateLogoutToken(tokenType: JoseTokenTypes.Jwt)
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

        var result = await ValidateAsync(validator, CreateLogoutToken(tokenType: string.Empty));

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderAdvertisesOnlySymmetricAlgorithm_Fails()
    {
        var validator = CreateValidator(null, advertised: ["HS256"]);

        var result = await ValidateAsync(validator, CreateLogoutToken());

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
            CreateLogoutToken(),
            CreateRegistration(["RS256"])
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
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
        Assert.Equal(TokenValidationFailureCodes.InvalidSignature, result.FailureCode);
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
            CreateStrictMock<IClientKeyRingProvider>().Object,
            ServiceProvider.GetRequiredService<IJwtValidator>(),
            CreateStrictMock<IExpiringKeySet>().Object
        );

        var result = await ValidateAsync(validator, string.Empty);

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
            CreateLogoutToken(includeSub: false, includeSid: false)
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

        var result = await ValidateAsync(validator, CreateLogoutToken(includeSub: false));

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Null(result.Subject);
        Assert.Equal("sid-1", result.SessionId);
    }

    [Fact]
    public async Task ValidateAsync_WhenOnlySub_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock(), CreateAcceptingKeySet());

        var result = await ValidateAsync(validator, CreateLogoutToken(includeSid: false));

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
            CreateLogoutToken(includeSid: false),
            registration
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
            CreateLogoutToken(extraClaims: new Dictionary<string, object> { ["sid"] = 42 })
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
            CreateLogoutToken(extraClaims: new Dictionary<string, object> { ["sub"] = 42 })
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

        var result = await ValidateAsync(validator, CreateLogoutToken(includeEvents: false));

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
            CreateLogoutToken(
                extraClaims: new Dictionary<string, object>
                {
                    ["events"] = new Dictionary<string, object>
                    {
                        ["other"] = new Dictionary<string, object>(),
                    },
                }
            )
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
            CreateLogoutToken(
                extraClaims: new Dictionary<string, object>
                {
                    ["events"] = new Dictionary<string, object> { [LogoutEvent] = "x" },
                }
            )
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
            CreateLogoutToken(extraClaims: new Dictionary<string, object> { ["nonce"] = "n" })
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal("The logout token contains a 'nonce' claim.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenJtiIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateLogoutToken(includeJti: false));

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
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        var validator = CreateValidator(CreateKeySetMock(), keySet);

        var result = await ValidateAsync(validator, CreateLogoutToken());

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.TokenReplayed, result.FailureCode);
        Assert.Equal("The logout token has already been handled.", result.FailureReason);
    }

    private Mock<IExpiringKeySet> CreateRecordingKeySet(
        List<string> lookups,
        List<(string Key, DateTimeOffset ExpiresWhen)> records,
        bool added = true
    )
    {
        var mock = CreateStrictMock<IExpiringKeySet>();
        mock.Setup(x => x.ContainsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => lookups.Add(key))
            .Returns(ValueTask.FromResult(false));
        mock.Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, DateTimeOffset, CancellationToken>(
                (key, expiresWhen, _) => records.Add((key, expiresWhen))
            )
            .Returns(ValueTask.FromResult(added));
        return mock;
    }

    private DefaultLogoutTokenValidator CreateRecordingValidator(Mock<IExpiringKeySet> keySet) =>
        new(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            CreateStrictMock<IProviderMetadataResolver>().Object,
            CreateStrictMock<IProviderKeySetResolver>().Object,
            CreateStrictMock<IClientKeyRingProvider>().Object,
            ServiceProvider.GetRequiredService<IJwtValidator>(),
            keySet.Object
        );

    [Fact]
    public async Task ValidateAsync_WhenValid_LooksUpTheScopedJtiWithoutRecordingIt()
    {
        var lookups = new List<string>();
        var records = new List<(string Key, DateTimeOffset ExpiresWhen)>();
        var validator = CreateValidator(
            CreateKeySetMock(),
            CreateRecordingKeySet(lookups, records)
        );

        var result = await ValidateAsync(validator, CreateLogoutToken());

        Assert.True(result.IsValid, result.FailureReason);
        var lookup = Assert.Single(lookups);
        Assert.StartsWith("logout_token_jti:", lookup);
        Assert.DoesNotContain("jti-1", lookup);
        Assert.Empty(records);
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
            await ValidateAsync(validator, CreateLogoutToken())
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
            await ValidateAsync(validator, CreateLogoutToken())
        );
    }

    #endregion

    #region TryRecordHandledAsync Tests

    [Fact]
    public async Task TryRecordHandledAsync_WhenValid_RecordsTheLookedUpKeyUntilExpiryPlusSkew()
    {
        var lookups = new List<string>();
        var records = new List<(string Key, DateTimeOffset ExpiresWhen)>();
        var validator = CreateValidator(
            CreateKeySetMock(),
            CreateRecordingKeySet(lookups, records)
        );
        var result = await ValidateAsync(validator, CreateLogoutToken());
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
        var validator = CreateValidator(
            CreateKeySetMock(),
            CreateRecordingKeySet(lookups, records, added: false)
        );
        var result = await ValidateAsync(validator, CreateLogoutToken());
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
        var lookups = new List<string>();
        var records = new List<(string Key, DateTimeOffset ExpiresWhen)>();
        var validator = CreateRecordingValidator(CreateRecordingKeySet(lookups, records));
        var payload = JsonDocument.Parse("{}").RootElement;
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
        var validator = CreateRecordingValidator(CreateRecordingKeySet([], []));
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
