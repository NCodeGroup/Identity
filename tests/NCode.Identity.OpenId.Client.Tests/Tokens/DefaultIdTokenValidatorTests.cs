// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.EdDsa;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class DefaultIdTokenValidatorTests : BaseTests, IAsyncLifetime
{
    private const string Issuer = "https://op.example.test";
    private const string ClientId = "client-1";
    private const string KeyRingName = "client-ring";
    private const string SignatureFailure =
        "The ID token signature could not be verified with the provider's signing keys.";

    private ServiceProvider ServiceProvider { get; }
    private RSA Rsa { get; } = RSA.Create(2048);
    private RSA EncryptionRsa { get; } = RSA.Create(2048);
    private RSA OtherRsa { get; } = RSA.Create(2048);
    private SecretKey RsaKey { get; }
    private SecretKey EncryptionKey { get; }
    private ClientRegistration Registration { get; } =
        new(Issuer, ClientId, OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

    public DefaultIdTokenValidatorTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var jose = serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        jose.AddJwtLibrary();
        jose.AddEdDsa();
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
        Ed25519Key = CreateOkpKey("ed25519", "Ed25519", 256);
        Ed448Key = CreateOkpKey("ed448", "Ed448", 456);
    }

    private SecretKey Ed25519Key { get; }
    private SecretKey Ed448Key { get; }

    private SecretKey CreateOkpKey(string keyId, string curve, int keySizeBits)
    {
        var catalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
        Assert.True(catalog.TryGetByJwkKeyType("OKP", out var descriptor));
        byte[] keyMaterial = [];
        var generated = descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters { KeySizeBits = keySizeBits, Curve = curve },
            bytes =>
            {
                keyMaterial = bytes.ToArray();
                return string.Empty;
            }
        );
        return descriptor.DeserializeKeyMaterial(
            new KeyMetadata { KeyId = keyId },
            generated.SecretEncoding,
            keyMaterial
        );
    }

    private SecretKey GetKey(string alg) =>
        alg switch
        {
            "Ed25519" => Ed25519Key,
            "Ed448" => Ed448Key,
            _ => RsaKey,
        };

    private static string ExpectedHash(string alg, string value)
    {
        var ascii = SecureEncoding.ASCII.GetBytes(value);
        var hash = alg switch
        {
            "Ed25519" => SHA512.HashData(ascii),
            "Ed448" => Shake256.HashData(ascii, 114),
            _ => SHA256.HashData(ascii),
        };
        return Base64Url.EncodeToString(hash.AsSpan(0, hash.Length / 2));
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

    private Mock<IProviderKeySetResolver> CreateKeySetMock(SecretKey? key = null)
    {
        var mock = CreateStrictMock<IProviderKeySetResolver>();
        mock.Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKeys(key ?? RsaKey))
            .Verifiable();
        return mock;
    }

    private Mock<IClientKeyRingProvider> CreateKeyRingMock(params SecretKey[] keys)
    {
        var mock = CreateStrictMock<IClientKeyRingProvider>();
        mock.Setup(x => x.GetKeyRing(KeyRingName)).Returns(CreateKeys(keys)).Verifiable();
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

    private DefaultIdTokenValidator CreateValidator(
        Mock<IProviderKeySetResolver>? keySetMock,
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

        return new DefaultIdTokenValidator(
            TimeProvider.System,
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            mockMetadata.Object,
            CreateProviderJwtValidator(keySetMock.Object, keyRingMock.Object)
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

    private sealed class IdTokenOptions
    {
        public string Alg { get; init; } = "PS256";
        public string Issuer { get; init; } = DefaultIdTokenValidatorTests.Issuer;
        public string Audience { get; init; } = ClientId;
        public TimeSpan? ExpiresIn { get; init; }
        public TimeSpan? IssuedOffset { get; init; }
        public string TokenType { get; init; } = JoseTokenTypes.Jwt;
        public bool IncludeSub { get; init; } = true;
        public IReadOnlyDictionary<string, object>? ExtraClaims { get; init; }
        public SecretKey? SigningKey { get; init; }
        public IReadOnlyDictionary<string, object>? ExtraHeaderClaims { get; init; }
        public JoseEncryptionCredentials? EncryptionCredentials { get; init; }
        public IReadOnlyDictionary<string, object>? ExtraEncryptionHeaderClaims { get; init; }
    }

    private sealed class ValidateOptions
    {
        public string? Nonce { get; init; }
        public TimeSpan? MaxAge { get; init; }
        public string? Subject { get; init; }
        public string? AccessToken { get; init; }
        public string? AuthorizationCode { get; init; }
        public string? State { get; init; }
        public ClientRegistration? Registration { get; init; }
    }

    private async ValueTask<string> CreateIdTokenAsync(
        IdTokenOptions options,
        CancellationToken cancellationToken
    )
    {
        var issuedAt = DateTimeOffset.UtcNow + (options.IssuedOffset ?? TimeSpan.Zero);
        var claims = new Dictionary<string, object>();
        if (options.IncludeSub)
            claims[JoseClaimNames.Payload.Sub] = "user-1";
        if (options.ExtraClaims is not null)
        {
            foreach (var (name, value) in options.ExtraClaims)
                claims[name] = value;
        }

        Assert.True(
            ServiceProvider
                .GetRequiredService<IAlgorithmCollectionProvider>()
                .Collection.TryGetSignatureAlgorithm(options.Alg, out var algorithm)
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
                    Expires = issuedAt + (options.ExpiresIn ?? TimeSpan.FromMinutes(5)),
                    ExtraPayloadClaims = claims,
                    ExtraSignatureHeaderClaims = options.ExtraHeaderClaims,
                    EncryptionCredentials = options.EncryptionCredentials,
                    ExtraEncryptionHeaderClaims = options.ExtraEncryptionHeaderClaims,
                },
                cancellationToken
            );
    }

    private static Dictionary<string, object> Claims(string name, object value) =>
        new() { [name] = value };

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

    private async ValueTask<IdTokenValidationResult> ValidateAsync(
        DefaultIdTokenValidator validator,
        string idToken,
        ValidateOptions options,
        CancellationToken cancellationToken
    ) =>
        await validator.ValidateAsync(
            options.Registration ?? Registration,
            new IdTokenValidationRequest
            {
                IdToken = idToken,
                Nonce = options.Nonce,
                MaxAge = options.MaxAge,
                Subject = options.Subject,
                AccessToken = options.AccessToken,
                AuthorizationCode = options.AuthorizationCode,
                State = options.State,
            },
            cancellationToken
        );

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenValid_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid);
        Assert.Null(result.FailureCode);
        Assert.Equal("user-1", result.Payload.GetProperty(JoseClaimNames.Payload.Sub).GetString());
    }

    [Fact]
    public async Task ValidateAsync_WhenValid_ReturnsTheIdentity()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid);
        Assert.NotNull(result.Identity);
        Assert.Equal("user-1", result.Identity.FindFirst(JoseClaimNames.Payload.Sub)?.Value);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { Issuer = "https://evil.example.test" },
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
            await CreateIdTokenAsync(
                new IdTokenOptions { Audience = "other-client" },
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
            await CreateIdTokenAsync(
                new IdTokenOptions { IssuedOffset = TimeSpan.FromMinutes(-30) },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.Expired, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuedInTheFuture_FailsAsNotYetValid()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { IssuedOffset = TimeSpan.FromHours(1) },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.NotYetValid, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenTypeIsLogoutJwt_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { TokenType = JoseTokenTypes.LogoutJwt },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidTokenType, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { IncludeSub = false },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubjectDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions { Subject = "user-2" },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.SubjectMismatch, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAzpIsNotTheClient_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("azp", "other-client") },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions { Nonce = "n-1" },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.NonceMismatch, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("nonce", "n-2") },
                CancellationToken.None
            ),
            new ValidateOptions { Nonce = "n-1" },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.NonceMismatch, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceMatches_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("nonce", "n-1") },
                CancellationToken.None
            ),
            new ValidateOptions { Nonce = "n-1" },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAuthenticationIsOlderThanMaxAge_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var authTime = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("auth_time", authTime) },
                CancellationToken.None
            ),
            new ValidateOptions { MaxAge = TimeSpan.FromMinutes(1) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.MaxAgeExceeded, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenMaxAgeIsNotExceeded_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var authTime = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("auth_time", authTime) },
                CancellationToken.None
            ),
            new ValidateOptions { MaxAge = TimeSpan.FromHours(1) },
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenMaxAgeAndAuthTimeIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions { MaxAge = TimeSpan.FromMinutes(1) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAuthTimeIsOutOfRange_FailsWithInvalidClaim()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("auth_time", 1e300) },
                CancellationToken.None
            ),
            new ValidateOptions { MaxAge = TimeSpan.FromMinutes(1) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
        Assert.Equal("The ID token has no valid 'auth_time' claim.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenAuthTimeIsAString_FailsWithInvalidClaim()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var authTime = DateTimeOffset
            .UtcNow.ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("auth_time", authTime) },
                CancellationToken.None
            ),
            new ValidateOptions { MaxAge = TimeSpan.FromHours(1) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAuthTimeIsFractional_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var authTime = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds() + 0.5;

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("auth_time", authTime) },
                CancellationToken.None
            ),
            new ValidateOptions { MaxAge = TimeSpan.FromHours(1) },
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenAuthTimeIsTheLatestNumericDate_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var authTime = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("auth_time", authTime) },
                CancellationToken.None
            ),
            new ValidateOptions { MaxAge = TimeSpan.FromHours(1) },
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Theory]
    [InlineData("PS256")]
    [InlineData("Ed25519")]
    [InlineData("Ed448")]
    public async Task ValidateAsync_WhenHashClaimsMatch_ReturnsPayload(string alg)
    {
        var key = GetKey(alg);
        var validator = CreateValidator(CreateKeySetMock(key));
        var claims = new Dictionary<string, object>
        {
            ["at_hash"] = ExpectedHash(alg, "access-token"),
            ["c_hash"] = ExpectedHash(alg, "auth-code"),
            ["s_hash"] = ExpectedHash(alg, "state-1"),
        };

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions
                {
                    Alg = alg,
                    ExtraClaims = claims,
                    SigningKey = key,
                },
                CancellationToken.None
            ),
            new ValidateOptions
            {
                AccessToken = "access-token",
                AuthorizationCode = "auth-code",
                State = "state-1",
            },
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Theory]
    [InlineData("PS256")]
    [InlineData("Ed25519")]
    [InlineData("Ed448")]
    public async Task ValidateAsync_WhenAtHashDiffers_Fails(string alg)
    {
        var key = GetKey(alg);
        var validator = CreateValidator(CreateKeySetMock(key));

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions
                {
                    Alg = alg,
                    ExtraClaims = Claims("at_hash", ExpectedHash(alg, "other")),
                    SigningKey = key,
                },
                CancellationToken.None
            ),
            new ValidateOptions { AccessToken = "access-token" },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.TokenHashMismatch, result.FailureCode);
        Assert.Equal("The ID token 'at_hash' does not match.", result.FailureReason);
    }

    [Theory]
    [InlineData("PS256")]
    [InlineData("Ed25519")]
    [InlineData("Ed448")]
    public async Task ValidateAsync_WhenCHashDiffers_Fails(string alg)
    {
        var key = GetKey(alg);
        var validator = CreateValidator(CreateKeySetMock(key));

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions
                {
                    Alg = alg,
                    ExtraClaims = Claims("c_hash", ExpectedHash(alg, "other")),
                    SigningKey = key,
                },
                CancellationToken.None
            ),
            new ValidateOptions { AuthorizationCode = "auth-code" },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.TokenHashMismatch, result.FailureCode);
        Assert.Equal("The ID token 'c_hash' does not match.", result.FailureReason);
    }

    [Theory]
    [InlineData("PS256")]
    [InlineData("Ed25519")]
    [InlineData("Ed448")]
    public async Task ValidateAsync_WhenSHashDiffers_Fails(string alg)
    {
        var key = GetKey(alg);
        var validator = CreateValidator(CreateKeySetMock(key));

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions
                {
                    Alg = alg,
                    ExtraClaims = Claims("s_hash", ExpectedHash(alg, "other")),
                    SigningKey = key,
                },
                CancellationToken.None
            ),
            new ValidateOptions { State = "state-1" },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.TokenHashMismatch, result.FailureCode);
        Assert.Equal("The ID token 's_hash' does not match.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenAtHashIsAbsent_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions { AccessToken = "access-token" },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAtHashIsNotAString_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("at_hash", 42) },
                CancellationToken.None
            ),
            new ValidateOptions { AccessToken = "access-token" },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.TokenHashMismatch, result.FailureCode);
        Assert.Equal("The ID token 'at_hash' does not match.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderAdvertisesOnlyAnotherAlgorithm_Fails()
    {
        var validator = CreateValidator(null, advertised: ["ES256"]);

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderAdvertisesOnlySymmetricAlgorithm_Fails()
    {
        var validator = CreateValidator(null, advertised: ["HS256"]);

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
        Assert.StartsWith("No ID-token signing algorithm", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenRegistrationPermitsOnlyAnotherAlgorithm_Fails()
    {
        var validator = CreateValidator(null);

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions { Registration = CreateRegistration(["RS256"]) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenRegistrationPermitsTheAlgorithm_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions { Registration = CreateRegistration(["PS256"]) },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenKidIsUnknownUntilRefresh_ReturnsPayload()
    {
        var mockKeySet = CreateStrictMock<IProviderKeySetResolver>();
        mockKeySet
            .Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKeys())
            .Verifiable();
        mockKeySet
            .Setup(x => x.RefreshSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKeys(RsaKey))
            .Verifiable();
        var validator = CreateValidator(mockKeySet);

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenKidIsUnknownAfterRefresh_Fails()
    {
        var mockKeySet = CreateStrictMock<IProviderKeySetResolver>();
        mockKeySet
            .Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKeys())
            .Verifiable();
        mockKeySet
            .Setup(x => x.RefreshSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKeys())
            .Verifiable();
        var validator = CreateValidator(mockKeySet);

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidSignature, result.FailureCode);
        Assert.Equal(SignatureFailure, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureIsTampered_FailsWithoutTokenText()
    {
        var validator = CreateValidator(CreateKeySetMock());
        var token = await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None);
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
                await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
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
                await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
                new ValidateOptions(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenIdTokenIsEmpty_Fails()
    {
        var validator = new DefaultIdTokenValidator(
            TimeProvider.System,
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            CreateStrictMock<IProviderMetadataResolver>().Object,
            CreateProviderJwtValidator(
                CreateStrictMock<IProviderKeySetResolver>().Object,
                CreateStrictMock<IClientKeyRingProvider>().Object
            )
        );

        var result = await ValidateAsync(
            validator,
            string.Empty,
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal("The ID token is empty.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenMetadataHasNoIssuer_Throws()
    {
        var mockMetadata = CreateStrictMock<IProviderMetadataResolver>();
        mockMetadata
            .Setup(x => x.GetMetadataAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenIdProviderMetadata())
            .Verifiable();
        var mockKeySet = CreateStrictMock<IProviderKeySetResolver>();
        var validator = new DefaultIdTokenValidator(
            TimeProvider.System,
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            mockMetadata.Object,
            CreateProviderJwtValidator(
                mockKeySet.Object,
                CreateStrictMock<IClientKeyRingProvider>().Object
            )
        );

        await Assert.ThrowsAsync<OpenIdProviderException>(async () =>
            await ValidateAsync(
                validator,
                await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
                new ValidateOptions(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenAzpIsNotAString_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions { ExtraClaims = Claims("azp", 42) },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidAudience, result.FailureCode);
        Assert.Equal(
            "The 'azp' claim of the ID token does not match the client.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenTypeIsNotAString_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            await CreateIdTokenAsync(
                new IdTokenOptions
                {
                    TokenType = string.Empty,
                    ExtraHeaderClaims = Claims("typ", 42),
                },
                CancellationToken.None
            ),
            new ValidateOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidTokenType, result.FailureCode);
        Assert.Equal("The 'typ' header of the ID token is not JWT.", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptedToTheClient_ReturnsPayload()
    {
        var validator = CreateValidator(
            CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
        Assert.Null(result.FailureCode);
        Assert.Equal("user-1", result.Payload.GetProperty(JoseClaimNames.Payload.Sub).GetString());
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
            await CreateIdTokenAsync(new IdTokenOptions(), CancellationToken.None),
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal(
            "The ID token is not encrypted, but the client registered ID-token encryption.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptedAndNoEncryptionIsRegistered_FailsWithAlgorithmNotPermitted()
    {
        var validator = CreateValidator(null);
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
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
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.DecryptionFailed, result.FailureCode);
        Assert.Equal(
            "The ID token could not be decrypted with the client's keys.",
            result.FailureReason
        );
        Assert.DoesNotContain(idToken, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenKeyRingHasNoEncryptionKey_FailsWithDecryptionFailed()
    {
        var signatureOnlyKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(
                new KeyMetadata { KeyId = "enc-1", Use = SecretKeyUses.Signature },
                EncryptionRsa
            );
        var validator = CreateValidator(null, keyRingMock: CreateKeyRingMock(signatureOnlyKey));
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.DecryptionFailed, result.FailureCode);
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
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                SigningKey = otherSigningKey,
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidSignature, result.FailureCode);
        Assert.Equal(SignatureFailure, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenContentEncryptionIsNotRegistered_FailsWithAlgorithmNotPermitted()
    {
        var validator = CreateValidator(null, keyRingMock: CreateKeyRingMock(EncryptionKey));
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(
                    EncryptionKey,
                    contentEncryption: "A256GCM"
                ),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenReplicatedIssuerDiffers_FailsWithInvalidIssuer()
    {
        var validator = CreateValidator(
            CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
                ExtraEncryptionHeaderClaims = Claims("iss", "https://evil.example.test"),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.InvalidIssuer, result.FailureCode);
        Assert.Equal(
            "The 'iss' header of the encrypted ID token does not match the issuer.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenReplicatedIssuerMatches_ReturnsPayload()
    {
        var validator = CreateValidator(
            CreateKeySetMock(),
            keyRingMock: CreateKeyRingMock(EncryptionKey)
        );
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
                ExtraEncryptionHeaderClaims = Claims("iss", Issuer),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions { Registration = CreateEncryptingRegistration() },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
        Assert.Null(result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptionIsRegisteredWithoutKeyRing_ThrowsInvalidOperationException()
    {
        var validator = CreateValidator(null);
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ValidateAsync(
                validator,
                idToken,
                new ValidateOptions { Registration = CreateEncryptingRegistration(keyRing: null) },
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenNoRegisteredEncryptionAlgorithmIsPermitted_FailsWithAlgorithmNotPermitted()
    {
        var validator = CreateValidator(null);
        var idToken = await CreateIdTokenAsync(
            new IdTokenOptions
            {
                EncryptionCredentials = CreateEncryptionCredentials(EncryptionKey),
            },
            CancellationToken.None
        );

        var result = await ValidateAsync(
            validator,
            idToken,
            new ValidateOptions
            {
                Registration = CreateEncryptingRegistration(keyManagementAlgorithms: ["RSA1_5"]),
            },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Equal(TokenValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
        Assert.Equal(
            "No ID-token encryption algorithm that the client registered is permitted.",
            result.FailureReason
        );
    }

    #endregion
}
