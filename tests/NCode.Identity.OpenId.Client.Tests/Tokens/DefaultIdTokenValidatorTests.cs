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
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class DefaultIdTokenValidatorTests : BaseTests, IAsyncLifetime
{
    private const string Issuer = "https://op.example.test";
    private const string ClientId = "client-1";
    private const string SignatureFailure =
        "The ID token signature could not be verified with the provider's signing keys.";

    private ServiceProvider ServiceProvider { get; }
    private RSA Rsa { get; } = RSA.Create(2048);
    private SecretKey RsaKey { get; }
    private ClientRegistration Registration { get; } =
        new(Issuer, ClientId, ClientAuthenticationMethods.PrivateKeyJwt);

    public DefaultIdTokenValidatorTests()
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

    private DefaultIdTokenValidator CreateValidator(
        Mock<IProviderKeySetResolver>? keySetMock,
        List<string>? advertised = null,
        OpenIdClientOptions? options = null
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

        return new DefaultIdTokenValidator(
            TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(options ?? new OpenIdClientOptions()),
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            mockMetadata.Object,
            keySetMock.Object,
            ServiceProvider.GetRequiredService<IJwtValidator>()
        );
    }

    private string CreateIdToken(
        string alg = "PS256",
        string issuer = Issuer,
        string audience = ClientId,
        TimeSpan? expiresIn = null,
        string tokenType = JoseTokenTypes.Jwt,
        bool includeSub = true,
        IReadOnlyDictionary<string, object>? extraClaims = null
    )
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new Dictionary<string, object>();
        if (includeSub)
            claims["sub"] = "user-1";
        if (extraClaims is not null)
        {
            foreach (var (name, value) in extraClaims)
                claims[name] = value;
        }

        Assert.True(
            ServiceProvider
                .GetRequiredService<IAlgorithmCollectionProvider>()
                .Collection.TryGetSignatureAlgorithm(alg, out var algorithm)
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
                    IssuedAt = now,
                    NotBefore = now,
                    Expires = now + (expiresIn ?? TimeSpan.FromMinutes(5)),
                    ExtraPayloadClaims = claims,
                }
            );
    }

    private static Dictionary<string, object> Claims(string name, object value) =>
        new() { [name] = value };

    private async ValueTask<IdTokenValidationResult> ValidateAsync(
        DefaultIdTokenValidator validator,
        string idToken,
        string? nonce = null,
        TimeSpan? maxAge = null,
        string? subject = null
    ) =>
        await validator.ValidateAsync(
            Registration,
            new IdTokenValidationRequest
            {
                IdToken = idToken,
                Nonce = nonce,
                MaxAge = maxAge,
                Subject = subject,
            },
            CancellationToken.None
        );

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenValid_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateIdToken());

        Assert.True(result.IsValid);
        Assert.Equal("user-1", result.Payload.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateIdToken(issuer: "https://evil.example.test")
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAudienceLacksTheClient_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateIdToken(audience: "other-client"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpired_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateIdToken(expiresIn: TimeSpan.FromMinutes(-10))
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenTypeIsLogoutJwt_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateIdToken(tokenType: JoseTokenTypes.LogoutJwt)
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateIdToken(includeSub: false));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubjectDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateIdToken(), subject: "user-2");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAzpIsNotTheClient_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateIdToken(extraClaims: Claims("azp", "other-client"))
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(validator, CreateIdToken(), nonce: "n-1");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceDiffers_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateIdToken(extraClaims: Claims("nonce", "n-2")),
            nonce: "n-1"
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceMatches_ReturnsPayload()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateIdToken(extraClaims: Claims("nonce", "n-1")),
            nonce: "n-1"
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
            CreateIdToken(extraClaims: Claims("auth_time", authTime)),
            maxAge: TimeSpan.FromMinutes(1)
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenMaxAgeAndAuthTimeIsMissing_Fails()
    {
        var validator = CreateValidator(CreateKeySetMock());

        var result = await ValidateAsync(
            validator,
            CreateIdToken(),
            maxAge: TimeSpan.FromMinutes(1)
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderAdvertisesOnlyAnotherAlgorithm_Fails()
    {
        var validator = CreateValidator(null, advertised: ["ES256"]);

        var result = await ValidateAsync(validator, CreateIdToken());

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderAdvertisesOnlySymmetricAlgorithm_Fails()
    {
        var validator = CreateValidator(null, advertised: ["HS256"]);

        var result = await ValidateAsync(validator, CreateIdToken());

        Assert.False(result.IsValid);
        Assert.StartsWith("No ID-token signing algorithm", result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenOptionPermitsOnlyAnotherAlgorithm_Fails()
    {
        var validator = CreateValidator(
            null,
            options: new OpenIdClientOptions { IdTokenSigningAlgorithms = ["RS256"] }
        );

        var result = await ValidateAsync(validator, CreateIdToken());

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenKidIsUnknownUntilRefresh_ReturnsPayload()
    {
        var mockKeySet = CreateStrictMock<IProviderKeySetResolver>();
        mockKeySet
            .Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(CreateKeys()))
            .Verifiable();
        mockKeySet
            .Setup(x => x.RefreshSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(CreateKeys(RsaKey)))
            .Verifiable();
        var validator = CreateValidator(mockKeySet);

        var result = await ValidateAsync(validator, CreateIdToken());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenKidIsUnknownAfterRefresh_Fails()
    {
        var mockKeySet = CreateStrictMock<IProviderKeySetResolver>();
        mockKeySet
            .Setup(x => x.GetSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(CreateKeys()))
            .Verifiable();
        mockKeySet
            .Setup(x => x.RefreshSigningKeysAsync(Issuer, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(CreateKeys()))
            .Verifiable();
        var validator = CreateValidator(mockKeySet);

        var result = await ValidateAsync(validator, CreateIdToken());

        Assert.False(result.IsValid);
        Assert.Equal(SignatureFailure, result.FailureReason);
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
            await ValidateAsync(validator, CreateIdToken())
        );
    }

    #endregion
}
