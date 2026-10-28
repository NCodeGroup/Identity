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
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.Jwt.Validation;

public sealed class DefaultJwtValidatorTests : IAsyncLifetime, IDisposable
{
    private const string Issuer = "https://issuer.example";

    private readonly DateTimeOffset _utcNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ServiceProvider _serviceProvider;
    private readonly RSA _clientRsa = RSA.Create(2048);
    private readonly RSA _serverRsa = RSA.Create(2048);

    public DefaultJwtValidatorTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<TimeProvider>(new TestTimeProvider(_utcNow));
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        SigningKey = CreateRsaKey("client-sig", _clientRsa, use: null, algorithm: null);
        EncryptionKey = CreateRsaKey("server-enc", _serverRsa, use: null, algorithm: null);
    }

    private IJwtEncoder Encoder => _serviceProvider.GetRequiredService<IJwtEncoder>();
    private IJwtValidator Validator => _serviceProvider.GetRequiredService<IJwtValidator>();
    private IJoseSerializer JoseSerializer =>
        _serviceProvider.GetRequiredService<IJoseSerializer>();

    private IAlgorithmCollection Algorithms =>
        _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;

    private SecretKey SigningKey { get; }
    private SecretKey EncryptionKey { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _serviceProvider.DisposeAsync();

    public void Dispose()
    {
        _clientRsa.Dispose();
        _serverRsa.Dispose();
    }

    private SecretKey CreateRsaKey(string keyId, RSA rsa, string? use, string? algorithm) =>
        _serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(
                new KeyMetadata
                {
                    KeyId = keyId,
                    Use = use,
                    Algorithm = algorithm,
                },
                rsa
            );

    private SecretKey CreateCertificateKey(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var request = new CertificateRequest(
            "CN=client",
            _clientRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);
        return _serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .Create(new KeyMetadata { KeyId = "client-cert" }, certificate);
    }

    private JoseSigningCredentials CreateSigningCredentials(
        SecretKey secretKey,
        string algorithmCode = AlgorithmCodes.DigitalSignature.RsaSha256
    ) =>
        Algorithms.TryGetSignatureAlgorithm(algorithmCode, out var algorithm)
            ? new JoseSigningCredentials(secretKey, algorithm)
            : throw new InvalidOperationException(algorithmCode);

    private JoseEncryptionCredentials CreateEncryptionCredentials(
        string keyManagementCode = AlgorithmCodes.KeyManagement.RsaOaep256,
        string contentEncryptionCode = AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,
        string? compressionCode = null
    )
    {
        if (!Algorithms.TryGetKeyManagementAlgorithm(keyManagementCode, out var keyManagement))
            throw new InvalidOperationException(keyManagementCode);

        if (
            !Algorithms.TryGetAuthenticatedEncryptionAlgorithm(
                contentEncryptionCode,
                out var contentEncryption
            )
        )
            throw new InvalidOperationException(contentEncryptionCode);

        CompressionAlgorithm? compression = null;
        if (
            compressionCode is not null
            && !Algorithms.TryGetCompressionAlgorithm(compressionCode, out compression)
        )
            throw new InvalidOperationException(compressionCode);

        return new JoseEncryptionCredentials(
            EncryptionKey,
            keyManagement,
            contentEncryption,
            compression
        );
    }

    private string EncodeToken(
        JoseSigningCredentials? signingCredentials,
        JoseEncryptionCredentials? encryptionCredentials
    ) =>
        Encoder.Encode(
            new JwtEncodingParameters
            {
                SigningCredentials = signingCredentials,
                EncryptionCredentials = encryptionCredentials,
                Issuer = Issuer,
                IssuedAt = _utcNow,
                NotBefore = _utcNow,
                Expires = _utcNow.AddMinutes(5),
            }
        );

    private string EncryptContent(string content)
    {
        var options = new JoseEncryptionOptions(CreateEncryptionCredentials())
        {
            ProtectedHeaders = new Dictionary<string, object>
            {
                [JoseClaimNames.Header.Cty] = JoseTokenTypes.Jwt,
            },
        };

        return JoseSerializer.CreateEncoder(options).Encode(content);
    }

    private static string CreateUnsecuredToken()
    {
        static string Encode(string json) =>
            System.Buffers.Text.Base64Url.EncodeToString(SecureEncoding.UTF8.GetBytes(json));

        var header = Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Encode($"{{\"iss\":\"{Issuer}\",\"sub\":\"attacker\"}}");
        return $"{header}.{payload}.";
    }

    private async Task<JwtValidationResult> ValidateAsync(
        string token,
        Action<JwtValidationParameters> configure
    )
    {
        var parameters = new JwtValidationParameters();
        configure(parameters);
        return await Validator.ValidateAsync(token, parameters, CancellationToken.None);
    }

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenSignedJws_Succeeds()
    {
        var token = EncodeToken(CreateSigningCredentials(SigningKey), encryptionCredentials: null);

        var result = await ValidateAsync(token, x => x.UseValidationKeys([SigningKey]));

        Assert.True(result.IsValid);
        var decodedJwt = Assert.IsType<DecodedJwt>(result.DecodedJwt);
        Assert.Same(SigningKey, decodedJwt.SigningKey);
        Assert.Null(decodedJwt.EncryptionHeader);
        Assert.Null(decodedJwt.DecryptionKey);
    }

    [Theory]
    [InlineData(-60, 60)]
    [InlineData(1, 60)]
    [InlineData(-60, -1)]
    public async Task ValidateAsync_WhenCertificateIsActiveOrWithinClockSkew_Succeeds(
        int notBeforeMinutes,
        int notAfterMinutes
    )
    {
        // The default clock skew is five minutes, so a certificate a minute from either edge of its validity is active.
        var certificateKey = CreateCertificateKey(
            _utcNow.AddMinutes(notBeforeMinutes),
            _utcNow.AddMinutes(notAfterMinutes)
        );
        var token = EncodeToken(
            CreateSigningCredentials(certificateKey),
            encryptionCredentials: null
        );

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([certificateKey]).ValidateCertificateLifeTime()
        );

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(60, 120, "not valid before")]
    [InlineData(-120, -60, "not valid after")]
    public async Task ValidateAsync_WhenCertificateIsOutsideItsValidity_Fails(
        int notBeforeMinutes,
        int notAfterMinutes,
        string expectedMessage
    )
    {
        var certificateKey = CreateCertificateKey(
            _utcNow.AddMinutes(notBeforeMinutes),
            _utcNow.AddMinutes(notAfterMinutes)
        );
        var token = EncodeToken(
            CreateSigningCredentials(certificateKey),
            encryptionCredentials: null
        );

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([certificateKey]).ValidateCertificateLifeTime()
        );

        Assert.False(result.IsValid);
        Assert.Contains(expectedMessage, result.Exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenUnsecuredJws_Fails()
    {
        var token = CreateUnsecuredToken();

        var result = await ValidateAsync(token, x => x.UseValidationKeys([SigningKey]));

        Assert.False(result.IsValid);
        Assert.Contains("none", result.Exception.Message, StringComparison.Ordinal);
        Assert.Equal(JwtValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenUnsecuredJwsAndEmptyKey_Fails()
    {
        var emptyKey = _serviceProvider.GetRequiredService<ISecretKeyFactory>().Empty;
        var token = CreateUnsecuredToken();

        var result = await ValidateAsync(token, x => x.UseValidationKeys([emptyKey]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNestedJwt_DecryptsWithDecryptionKeysAndVerifiesWithValidationKeys()
    {
        var token = EncodeToken(
            CreateSigningCredentials(SigningKey),
            CreateEncryptionCredentials()
        );

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([SigningKey]).UseDecryptionKeys([EncryptionKey])
        );

        Assert.True(result.IsValid);
        var decodedJwt = Assert.IsType<DecodedJwt>(result.DecodedJwt);
        Assert.Same(SigningKey, decodedJwt.SigningKey);
        Assert.Same(EncryptionKey, decodedJwt.DecryptionKey);
        Assert.Equal(
            AlgorithmCodes.DigitalSignature.RsaSha256,
            decodedJwt.Header.GetProperty(JoseClaimNames.Header.Alg).GetString()
        );
        var encryptionHeader = Assert.NotNull(decodedJwt.EncryptionHeader);
        Assert.Equal(
            AlgorithmCodes.KeyManagement.RsaOaep256,
            encryptionHeader.GetProperty(JoseClaimNames.Header.Alg).GetString()
        );
        Assert.Equal(Issuer, decodedJwt.Payload.GetProperty("iss").GetString());
    }

    [Fact]
    public async Task ValidateAsync_WhenNestedJwtWithoutDecryptionKeys_Fails()
    {
        var token = EncodeToken(
            CreateSigningCredentials(SigningKey),
            CreateEncryptionCredentials()
        );

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([SigningKey, EncryptionKey])
        );

        Assert.False(result.IsValid);
        Assert.Contains("decryption keys", result.Exception.Message, StringComparison.Ordinal);
        Assert.Equal(JwtValidationFailureCodes.InvalidToken, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenEncryptedWithoutSignature_Fails()
    {
        var token = EncodeToken(signingCredentials: null, CreateEncryptionCredentials());

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([SigningKey]).UseDecryptionKeys([EncryptionKey])
        );

        Assert.False(result.IsValid);
        Assert.Contains("Nested JWT", result.Exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenNestedJwtEnclosesEncryptedToken_Fails()
    {
        var innerToken = EncodeToken(signingCredentials: null, CreateEncryptionCredentials());
        var token = EncryptContent(innerToken);

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([SigningKey]).UseDecryptionKeys([EncryptionKey])
        );

        Assert.False(result.IsValid);
        Assert.Contains("JWS", result.Exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenNestedJwtEnclosesUnsecuredJws_Fails()
    {
        var token = EncryptContent(CreateUnsecuredToken());

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([SigningKey]).UseDecryptionKeys([EncryptionKey])
        );

        Assert.False(result.IsValid);
        Assert.Contains("none", result.Exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenJsonSerialization_Fails()
    {
        var options = new JoseSigningOptions(CreateSigningCredentials(SigningKey))
        {
            Format = JoseSerializationFormat.Flattened,
        };
        var token = JoseSerializer
            .CreateEncoder(options)
            .Encode(new Dictionary<string, object> { ["iss"] = Issuer });

        var result = await ValidateAsync(token, x => x.UseValidationKeys([SigningKey]));

        Assert.False(result.IsValid);
        Assert.Contains("compact", result.Exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { AlgorithmCodes.DigitalSignature.RsaSha256 }, true)]
    [InlineData(new[] { AlgorithmCodes.DigitalSignature.RsaSsaPssSha256 }, false)]
    [InlineData(new string[0], false)]
    public async Task ValidateAsync_WhenSignatureAlgorithmsSet_PermitsOnlyThoseNamed(
        string[] permitted,
        bool expected
    )
    {
        var token = EncodeToken(CreateSigningCredentials(SigningKey), encryptionCredentials: null);

        var result = await ValidateAsync(
            token,
            x =>
            {
                x.UseValidationKeys([SigningKey]);
                x.ValidSignatureAlgorithms = permitted;
            }
        );

        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(new[] { AlgorithmCodes.KeyManagement.RsaOaep256 }, true)]
    [InlineData(new[] { AlgorithmCodes.KeyManagement.RsaOaep }, false)]
    [InlineData(new string[0], false)]
    public async Task ValidateAsync_WhenKeyManagementAlgorithmsSet_PermitsOnlyThoseNamed(
        string[] permitted,
        bool expected
    )
    {
        var token = EncodeToken(
            CreateSigningCredentials(SigningKey),
            CreateEncryptionCredentials()
        );

        var result = await ValidateAsync(
            token,
            x =>
            {
                x.UseValidationKeys([SigningKey]).UseDecryptionKeys([EncryptionKey]);
                x.ValidKeyManagementAlgorithms = permitted;
            }
        );

        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(new[] { AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256 }, true)]
    [InlineData(new[] { AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm }, false)]
    public async Task ValidateAsync_WhenContentEncryptionAlgorithmsSet_PermitsOnlyThoseNamed(
        string[] permitted,
        bool expected
    )
    {
        var token = EncodeToken(
            CreateSigningCredentials(SigningKey),
            CreateEncryptionCredentials()
        );

        var result = await ValidateAsync(
            token,
            x =>
            {
                x.UseValidationKeys([SigningKey]).UseDecryptionKeys([EncryptionKey]);
                x.ValidContentEncryptionAlgorithms = permitted;
            }
        );

        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(
        AlgorithmCodes.Compression.Deflate,
        new[] { AlgorithmCodes.Compression.Deflate },
        true
    )]
    [InlineData(AlgorithmCodes.Compression.Deflate, new string[0], false)]
    [InlineData(null, new string[0], true)]
    public async Task ValidateAsync_WhenCompressionAlgorithmsSet_PermitsOnlyThoseNamed(
        string? compression,
        string[] permitted,
        bool expected
    )
    {
        var token = EncodeToken(
            CreateSigningCredentials(SigningKey),
            CreateEncryptionCredentials(compressionCode: compression)
        );

        var result = await ValidateAsync(
            token,
            x =>
            {
                x.UseValidationKeys([SigningKey]).UseDecryptionKeys([EncryptionKey]);
                x.ValidCompressionAlgorithms = permitted;
            }
        );

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureAlgorithmIsNotPermitted_FailsWithAlgorithmNotPermitted()
    {
        var token = EncodeToken(CreateSigningCredentials(SigningKey), encryptionCredentials: null);

        var result = await ValidateAsync(
            token,
            x =>
            {
                x.UseValidationKeys([SigningKey]);
                x.ValidSignatureAlgorithms = [AlgorithmCodes.DigitalSignature.RsaSsaPssSha256];
            }
        );

        Assert.False(result.IsValid);
        Assert.Equal(JwtValidationFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoDecryptionKeyDecrypts_FailsWithDecryptionFailed()
    {
        var token = EncodeToken(
            CreateSigningCredentials(SigningKey),
            CreateEncryptionCredentials()
        );

        var result = await ValidateAsync(
            token,
            x => x.UseValidationKeys([SigningKey]).UseDecryptionKeys([SigningKey])
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtDecodeException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.DecryptionFailed, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenKeyDeclaresAnotherUse_DoesNotVerifyWithIt()
    {
        var encryptionOnlyKey = CreateRsaKey(
            "client-enc",
            _clientRsa,
            SecretKeyUses.Encryption,
            algorithm: null
        );
        var token = EncodeToken(CreateSigningCredentials(SigningKey), encryptionCredentials: null);

        var result = await ValidateAsync(token, x => x.UseValidationKeys([encryptionOnlyKey]));

        Assert.False(result.IsValid);
        Assert.IsType<JwtSecretKeyNotFoundException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.KeyNotFound, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenKeyDeclaresAnotherAlgorithm_DoesNotVerifyWithIt()
    {
        var pssOnlyKey = CreateRsaKey(
            "client-ps256",
            _clientRsa,
            use: null,
            AlgorithmCodes.DigitalSignature.RsaSsaPssSha256
        );
        var token = EncodeToken(CreateSigningCredentials(SigningKey), encryptionCredentials: null);

        var result = await ValidateAsync(token, x => x.UseValidationKeys([pssOnlyKey]));

        Assert.False(result.IsValid);
        Assert.IsType<JwtSecretKeyNotFoundException>(result.Exception);
    }

    #endregion

    #region Encode Tests

    [Fact]
    public void Encode_WhenSignedAndEncrypted_MarksEnclosingJweAsJwt()
    {
        var token = EncodeToken(
            CreateSigningCredentials(SigningKey),
            CreateEncryptionCredentials()
        );

        var header = JoseSerializer.Parse(token).Header;

        Assert.True(header.TryGetPropertyValue<string>(JoseClaimNames.Header.Cty, out var cty));
        Assert.Equal(JoseTokenTypes.Jwt, cty);
    }

    [Fact]
    public void Encode_WhenOnlyEncrypted_OmitsContentType()
    {
        var token = EncodeToken(signingCredentials: null, CreateEncryptionCredentials());

        var header = JoseSerializer.Parse(token).Header;

        Assert.False(header.TryGetProperty(JoseClaimNames.Header.Cty, out _));
    }

    #endregion
}
