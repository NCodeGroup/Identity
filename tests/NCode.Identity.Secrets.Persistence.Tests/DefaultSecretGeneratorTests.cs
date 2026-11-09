// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretGeneratorTests : BaseTests
{
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("tenant-one");

    private static readonly RemoteSigningAlgorithm RsaRow = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "RS256",
        SecretType = SecretTypes.Rsa,
        KeySizeBits = 2048,
        KeySpec = "RSA_2048",
        SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
    };

    // Asks for a larger key than the 2048-bit key the tests make the provider return.
    private static readonly RemoteSigningAlgorithm LargeRsaRow = RsaRow with
    {
        KeySizeBits = 4096,
        KeySpec = "RSA_4096",
    };

    private static readonly RemoteSigningAlgorithm EccRow = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "ES256",
        SecretType = SecretTypes.Ecc,
        KeySizeBits = 256,
        Curve = "P-256",
        KeySpec = "ECC_NIST_P256",
        SigningAlgorithm = "ECDSA_SHA_256",
        IsDerEncodedSignature = true,
    };

    // Asks for a curve other than the P-256 curve of the key the tests make the provider return.
    private static readonly RemoteSigningAlgorithm OtherCurveEccRow = EccRow with
    {
        Curve = "secp256k1",
        KeySpec = "ECC_SECG_P256K1",
    };

    private static readonly RemoteDecryptionAlgorithm OaepRow = new()
    {
        Provider = RemoteKeyProviderNames.AwsKms,
        Algorithm = "RSA-OAEP-256",
        SecretType = SecretTypes.Rsa,
        KeySizeBits = 2048,
        KeySpec = "RSA_2048",
        EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
    };

    private readonly DefaultSecretKeyTypeCatalog _catalog;
    private readonly DefaultSecretVerifierSchemeCatalog _verifierCatalog;
    private readonly Mock<IRemoteKeyProvider> _remoteProvider;
    private readonly DefaultRemoteKeyProviderCatalog _remoteCatalog;
    private readonly DefaultRemoteKeyDescriptorCatalog _remoteDescriptorCatalog;
    private readonly Mock<IRemoteSecretDestroyer> _remoteDestroyer;
    private readonly DefaultSecretGenerator _generator;
    private readonly DefaultSecretSerializer _serializer;

    public DefaultSecretGeneratorTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        var factoryProtector = provider.CreateProtector("factory");

        var secretKeyFactory = new DefaultSecretKeyFactory(
            new TestDataProtectorFactory<SecretKey>(factoryProtector),
            new DefaultEccCurveCatalog([])
        );

        var catalog = new DefaultSecretKeyTypeCatalog([
            new SymmetricSecretKeyTypeDescriptor(secretKeyFactory),
            new RsaSecretKeyTypeDescriptor(secretKeyFactory),
            new EccSecretKeyTypeDescriptor(secretKeyFactory, new DefaultEccCurveCatalog([])),
        ]);

        _catalog = catalog;
        _verifierCatalog = TestSecretVerifierSchemes.CreateCatalog();

        _remoteProvider = CreateStrictMock<IRemoteKeyProvider>();
        _remoteProvider.SetupGet(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();

        var source = CreateStrictMock<IRemoteKeyDescriptorSource>();
        source
            .SetupGet(x => x.Providers)
            .Returns([new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }])
            .Verifiable();
        source
            .SetupGet(x => x.Algorithms)
            .Returns([RsaRow, LargeRsaRow, EccRow, OtherCurveEccRow, OaepRow])
            .Verifiable();
        _remoteDescriptorCatalog = new DefaultRemoteKeyDescriptorCatalog([source.Object]);
        _remoteCatalog = new DefaultRemoteKeyProviderCatalog(
            [_remoteProvider.Object],
            _remoteDescriptorCatalog
        );

        _remoteDestroyer = CreateStrictMock<IRemoteSecretDestroyer>();

        _generator = CreateGenerator(new SharedSecretVerifierOptions());
        _serializer = new DefaultSecretSerializer(
            catalog,
            _verifierCatalog,
            new DefaultRemoteKeyProviderCatalog([], new DefaultRemoteKeyDescriptorCatalog([])),
            new DefaultRemoteKeyDescriptorCatalog([])
        );
    }

    private DefaultSecretGenerator CreateGenerator(
        SharedSecretVerifierOptions options,
        string? deployment = "test"
    ) =>
        new(
            _catalog,
            _verifierCatalog,
            Microsoft.Extensions.Options.Options.Create(options),
            new DefaultRemoteKeyResolver(_catalog, _remoteCatalog, _remoteDescriptorCatalog),
            _remoteDestroyer.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RemoteKeys = { Deployment = deployment } }
            )
        );

    // Opens the envelope of a stored verifier to its text.
    private static string ReadVerifierText(DefaultSecretProtector protector, PersistedSecret secret)
    {
        IBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
        protector.Unprotect(
            secret.EncodedValue,
            SecretProtectionContext.ForPersistedSecret(secret.SecretId),
            ref writer
        );
        return SecureEncoding.ASCII.GetString(((ArrayBufferWriter<byte>)writer).WrittenSpan);
    }

    private SecretVerifier ReadVerifier(DefaultSecretProtector protector, PersistedSecret secret) =>
        Assert.Single(_serializer.DeserializeVerifiers(protector, [secret]));

    private static GenerateSecretRequest CreateRequest(string secretType, int keySizeBits) =>
        new()
        {
            SecretId = "gen-1",
            SecretType = secretType,
            GenerationParameters = new SecretKeyGenerationParameters { KeySizeBits = keySizeBits },
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private static DefaultSecretProtector CreateProtector()
    {
        var keyId = Guid.NewGuid();
        var masterKey = MasterKey.Create(
            keyId,
            TenantOwner,
            1,
            RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes)
        );
        var keys = new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary();
        return new DefaultSecretProtector(TenantOwner, masterKey, keys);
    }

    private static SharedSecretRequest CreateImportRequest(string secretId) =>
        new()
        {
            SecretId = secretId,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private static SharedSecretRequest CreateSharedRequest(
        int entropyBits = SharedSecretPolicy.MinimumEntropyBits
    ) =>
        new()
        {
            SecretId = "gen-1",
            Use = "sig",
            Algorithm = "HS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            EntropyBits = entropyBits,
        };

    #region Constructor Tests

    [Fact]
    public void SharedSecretVerifierOptions_DefaultsToTheSchemesAndTheIterationsOfThePasswordHashes()
    {
        var options = new SharedSecretVerifierOptions();
        var pbkdf2Options = new Pbkdf2SecretVerifierOptions();

        Assert.Equal(SecretVerifierSchemes.Sha256, options.GeneratedSecretScheme);
        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, options.SuppliedSecretScheme);

        // OWASP's recommendation for PBKDF2-HMAC-SHA256, as the family's password hashes use.
        Assert.Equal(600_000, pbkdf2Options.Iterations);
        Assert.Equal(
            Pbkdf2SecretVerifierOptions.MinimumProductionIterations,
            pbkdf2Options.Iterations
        );
        Assert.Equal(
            Math.Max(1, Environment.ProcessorCount / 2),
            new SecretVerificationGateOptions().MaxConcurrency
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenASchemeIsNotRegistered_Throws(bool generated)
    {
        var options = new SharedSecretVerifierOptions();
        if (generated)
        {
            options.GeneratedSecretScheme = "unregistered";
        }
        else
        {
            options.SuppliedSecretScheme = "unregistered";
        }

        var exception = Assert.Throws<InvalidOperationException>(() => CreateGenerator(options));

        Assert.Contains("'unregistered'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Constructor_UsesTheSchemesTheOptionsName()
    {
        // Either scheme may make either kind of verifier; the options decide which.
        var generator = CreateGenerator(
            new SharedSecretVerifierOptions
            {
                GeneratedSecretScheme = SecretVerifierSchemes.Pbkdf2Sha256,
                SuppliedSecretScheme = SecretVerifierSchemes.Sha256,
            }
        );
        var protector = CreateProtector();
        var secretValue = "my-chosen-client-secret"u8.ToArray();

        var generated = generator.GenerateSharedSecretVerifier(protector, CreateSharedRequest());
        var imported = generator.ImportSharedSecretVerifier(
            protector,
            CreateImportRequest("import-5"),
            secretValue
        );

        Assert.Equal(
            SecretVerifierSchemes.Pbkdf2Sha256,
            ReadVerifier(protector, generated.Secret).Scheme
        );
        var importedVerifier = ReadVerifier(protector, imported);
        Assert.Equal(SecretVerifierSchemes.Sha256, importedVerifier.Scheme);
        Assert.Equal(
            SecretVerificationResult.Success,
            await importedVerifier.VerifyAsync(secretValue, CancellationToken.None)
        );
    }

    #endregion

    #region GenerateSecret Symmetric Tests

    [Fact]
    public void GenerateSecret_WhenTheRequestNamesNoLifecycle_ActivatesAtCreationAndRetiresAtExpiry()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 256);

        var persisted = _generator.GenerateSecret(protector, request).Secret;

        Assert.Equal(request.CreatedWhen, persisted.ActivatesWhen);
        Assert.Equal(request.ExpiresWhen, persisted.RetiresWhen);
    }

    [Fact]
    public void GenerateSecret_WhenTheRequestNamesALifecycle_StoresIt()
    {
        var protector = CreateProtector();
        var request = new GenerateSecretRequest
        {
            SecretId = "gen-1",
            SecretType = SecretTypes.Symmetric,
            GenerationParameters = new SecretKeyGenerationParameters { KeySizeBits = 256 },
            Use = "sig",
            Algorithm = "HS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
        };

        var persisted = _generator.GenerateSecret(protector, request).Secret;

        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), persisted.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(92), persisted.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(99), persisted.ExpiresWhen);
    }

    [Fact]
    public void GenerateSecret_WhenTheRequestNamesNoManager_LeavesTheSecretToItsCreator()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 256);

        var persisted = _generator.GenerateSecret(protector, request).Secret;

        Assert.Null(persisted.ManagedBy);
    }

    [Fact]
    public void GenerateSecret_WhenTheRequestNamesAManager_StoresIt()
    {
        var protector = CreateProtector();
        var request = new GenerateSecretRequest
        {
            SecretId = "gen-1",
            SecretType = SecretTypes.Symmetric,
            GenerationParameters = new SecretKeyGenerationParameters { KeySizeBits = 256 },
            Use = "sig",
            Algorithm = "HS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            ManagedBy = "rotation",
        };

        var persisted = _generator.GenerateSecret(protector, request).Secret;

        Assert.Equal("rotation", persisted.ManagedBy);
    }

    [Fact]
    public void GenerateSecret_WhenTheLifecycleIsOutOfOrder_ThrowsArgumentException()
    {
        var protector = CreateProtector();
        var request = new GenerateSecretRequest
        {
            SecretId = "gen-1",
            SecretType = SecretTypes.Symmetric,
            GenerationParameters = new SecretKeyGenerationParameters { KeySizeBits = 256 },
            Use = "sig",
            Algorithm = "HS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(100),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            _generator.GenerateSecret(protector, request)
        );

        Assert.Contains("cannot expire", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateSharedSecretVerifier_ActivatesAtCreationAndRetiresAtExpiry()
    {
        var protector = CreateProtector();
        var request = CreateSharedRequest();

        var persisted = _generator.GenerateSharedSecretVerifier(protector, request).Secret;

        Assert.Equal(request.CreatedWhen, persisted.ActivatesWhen);
        Assert.Equal(request.ExpiresWhen, persisted.RetiresWhen);
    }

    [Fact]
    public void GenerateSecret_WhenSymmetric_ProducesRoundTrippableSecret()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 256);

        var generated = _generator.GenerateSecret(protector, request);
        var persisted = generated.Secret;

        Assert.Equal("gen-1", persisted.SecretId);
        Assert.Equal(SecretTypes.Symmetric, persisted.SecretType);
        Assert.Equal(256, persisted.KeySizeBits);
        Assert.NotEmpty(persisted.EncodedValue);

        // ADR-1050: symmetric secrets reveal their one-time unprotected material, distinct from the protected value.
        Assert.NotNull(generated.SecretMaterial);
        Assert.NotEmpty(generated.SecretMaterial);
        Assert.NotEqual(persisted.EncodedValue, generated.SecretMaterial);

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        Assert.IsAssignableFrom<SymmetricSecretKey>(secretKey);
        Assert.Equal(256, secretKey.KeySizeBits);
    }

    [Fact]
    public void GenerateSecret_WhenSymmetricSizeNotMultipleOfEight_Throws()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 100);

        Assert.Throws<ArgumentException>(() => _generator.GenerateSecret(protector, request));
    }

    #endregion

    #region GenerateSharedSecretVerifier Tests

    public static TheoryData<bool> BothSharedSecretForms => new() { false, true };

    private GeneratedSecret GenerateSharedSecret(
        DefaultSecretProtector protector,
        SharedSecretRequest request,
        bool reversible
    ) =>
        reversible
            ? _generator.GenerateReversibleSharedSecret(protector, request)
            : _generator.GenerateSharedSecretVerifier(protector, request);

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void GenerateSharedSecret_WhenSizeNotMultipleOfEight_Throws(bool reversible)
    {
        var protector = CreateProtector();
        var request = CreateSharedRequest(100);

        Assert.Throws<ArgumentException>(() =>
            GenerateSharedSecret(protector, request, reversible)
        );
    }

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void GenerateSharedSecret_WhenEntropyIsBelowTheMinimum_Throws(bool reversible)
    {
        var protector = CreateProtector();
        var request = CreateSharedRequest(128);

        Assert.Throws<ArgumentException>(() =>
            GenerateSharedSecret(protector, request, reversible)
        );
    }

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void GenerateSharedSecret_RecordsThatTheServerGeneratedIt(bool reversible)
    {
        var protector = CreateProtector();

        var generated = GenerateSharedSecret(protector, CreateSharedRequest(384), reversible);

        Assert.Equal(SecretOrigins.Generated, generated.Secret.Origin);
        Assert.Equal("sig", generated.Secret.Use);
        Assert.Equal("HS256", generated.Secret.Algorithm);
        Assert.Equal(DateTimeOffset.UnixEpoch, generated.Secret.CreatedWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddYears(1), generated.Secret.ExpiresWhen);

        // 384 bits of entropy are 48 octets, whose base64url text is 64 characters.
        Assert.Equal(64, generated.SecretMaterial?.Length);
    }

    #endregion

    #region ImportSharedSecretVerifier Tests

    [Fact]
    public async Task ImportSharedSecret_StoresAVerifierOfTheSameShapeAsGenerateSharedSecret()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateSharedSecretVerifier(protector, CreateSharedRequest());
        var secretValue = SecureEncoding.UTF8.GetBytes(generated.SecretMaterial!);

        var imported = _generator.ImportSharedSecretVerifier(
            protector,
            CreateImportRequest("import-4"),
            secretValue
        );

        Assert.Equal(generated.Secret.SecretType, imported.SecretType);
        Assert.Equal(SecretEncodings.Verifier, generated.Secret.SecretEncoding);
        Assert.Equal(SecretEncodings.Verifier, imported.SecretEncoding);
        Assert.Equal(generated.Secret.KeySizeBits, imported.KeySizeBits);

        // A generated secret is verified by a digest, a supplied one by a password hash; both confirm the same secret.
        var generatedVerifier = ReadVerifier(protector, generated.Secret);
        var importedVerifier = ReadVerifier(protector, imported);
        Assert.Equal(SecretVerifierSchemes.Sha256, generatedVerifier.Scheme);
        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, importedVerifier.Scheme);
        Assert.Equal(
            SecretVerificationResult.Success,
            await generatedVerifier.VerifyAsync(secretValue, CancellationToken.None)
        );
        Assert.Equal(
            SecretVerificationResult.Success,
            await importedVerifier.VerifyAsync(secretValue, CancellationToken.None)
        );
    }

    [Fact]
    public void ImportReversibleSharedSecret_ProducesTheSameStoredShapeAsGenerateReversibleSharedSecret()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateReversibleSharedSecret(protector, CreateSharedRequest());

        var imported = _generator.ImportReversibleSharedSecret(
            protector,
            CreateImportRequest("import-3"),
            SecureEncoding.UTF8.GetBytes(generated.SecretMaterial!)
        );

        Assert.Equal(generated.Secret.SecretType, imported.SecretType);
        Assert.Equal(generated.Secret.SecretEncoding, imported.SecretEncoding);
        Assert.Equal(generated.Secret.KeySizeBits, imported.KeySizeBits);

        var generatedKey = Assert.IsAssignableFrom<SymmetricSecretKey>(
            _serializer.DeserializeSecret(protector, generated.Secret)
        );
        var importedKey = Assert.IsAssignableFrom<SymmetricSecretKey>(
            _serializer.DeserializeSecret(protector, imported)
        );

        var generatedWriter = new ArrayBufferWriter<byte>();
        generatedKey.ExportPrivateKey(ref generatedWriter);
        var importedWriter = new ArrayBufferWriter<byte>();
        importedKey.ExportPrivateKey(ref importedWriter);

        Assert.Equal(generatedWriter.WrittenSpan.ToArray(), importedWriter.WrittenSpan.ToArray());
    }

    #endregion

    #region GenerateSecret Rsa Tests

    [Fact]
    public void GenerateSecret_WhenRsa_ProducesRoundTrippableSecret()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Rsa, 2048);

        var generated = _generator.GenerateSecret(protector, request);
        var persisted = generated.Secret;

        Assert.Equal(SecretTypes.Rsa, persisted.SecretType);
        Assert.Equal(2048, persisted.KeySizeBits);

        // ADR-1050: asymmetric private keys are never revealed.
        Assert.Null(generated.SecretMaterial);

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        Assert.Equal(2048, secretKey.KeySizeBits);
    }

    #endregion

    #region GenerateSecret Ecc Tests

    [Fact]
    public void GenerateSecret_WhenEcc_ProducesRoundTrippableSecret()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Ecc, 256);

        var generated = _generator.GenerateSecret(protector, request);
        var persisted = generated.Secret;

        Assert.Equal(SecretTypes.Ecc, persisted.SecretType);
        Assert.Equal(256, persisted.KeySizeBits);

        // ADR-1050: asymmetric private keys are never revealed.
        Assert.Null(generated.SecretMaterial);

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        Assert.IsAssignableFrom<EccSecretKey>(secretKey);
        Assert.Equal(256, secretKey.KeySizeBits);
    }

    [Fact]
    public void GenerateSecret_WhenEccSizeUnsupported_Throws()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Ecc, 300);

        Assert.Throws<ArgumentException>(() => _generator.GenerateSecret(protector, request));
    }

    #endregion

    #region GenerateSecret Metadata Tests

    [Fact]
    public void GenerateSecret_CopiesMetadataFromRequest()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 256);

        var persisted = _generator.GenerateSecret(protector, request).Secret;

        Assert.Equal(request.Use, persisted.Use);
        Assert.Equal(request.Algorithm, persisted.Algorithm);
        Assert.Equal(request.CreatedWhen, persisted.CreatedWhen);
        Assert.Equal(request.ExpiresWhen, persisted.ExpiresWhen);
    }

    [Fact]
    public void GenerateSecret_WhenUnsupportedSecretType_Throws()
    {
        var protector = CreateProtector();
        var request = CreateRequest("x509", 2048);

        Assert.Throws<InvalidOperationException>(() =>
            _generator.GenerateSecret(protector, request)
        );
    }

    #endregion

    #region Public Key Export Tests

    [Fact]
    public void GenerateSecret_WhenRsa_ExportsSubjectPublicKeyInfoPem()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateSecret(protector, CreateRequest(SecretTypes.Rsa, 2048));

        var secretKey = _serializer.DeserializeSecret(protector, generated.Secret);

        var asymmetric = Assert.IsAssignableFrom<AsymmetricSecretKey>(secretKey);
        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", asymmetric.ExportSubjectPublicKeyInfoPem());
    }

    [Fact]
    public void GenerateSecret_WhenRsa_StoresTheSubjectPublicKeyInfo()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateSecret(protector, CreateRequest(SecretTypes.Rsa, 2048));

        var asymmetric = Assert.IsAssignableFrom<AsymmetricSecretKey>(
            _serializer.DeserializeSecret(protector, generated.Secret)
        );
        var writer = new ArrayBufferWriter<byte>();
        asymmetric.ExportSubjectPublicKeyInfo(ref writer);

        Assert.NotNull(generated.Secret.PublicKey);
        Assert.Equal(
            writer.WrittenSpan.ToArray(),
            System.Buffers.Text.Base64Url.DecodeFromChars(generated.Secret.PublicKey)
        );
    }

    [Fact]
    public void GenerateSharedSecret_HasNoPublicKey()
    {
        var protector = CreateProtector();

        var generated = _generator.GenerateSharedSecretVerifier(protector, CreateSharedRequest());

        Assert.Null(generated.Secret.PublicKey);
    }

    private static GenerateSecretRequest CreateEccRequest(int keySizeBits, string curve) =>
        new()
        {
            SecretId = "gen-ecc",
            SecretType = SecretTypes.Ecc,
            GenerationParameters = new SecretKeyGenerationParameters
            {
                KeySizeBits = keySizeBits,
                Curve = curve,
            },
            Use = "sig",
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    [Fact]
    public void GenerateSecret_WhenEccWithCurveName_GeneratesOnThatCurve()
    {
        var protector = CreateProtector();

        var persisted = _generator.GenerateSecret(protector, CreateEccRequest(384, "P-384")).Secret;

        Assert.Equal(384, persisted.KeySizeBits);
    }

    // A curve fixes the size of its keys, so a curve requested with another size generates nothing.
    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void GenerateSecret_WhenEccCurveNameHasAnotherSize_Throws(int keySizeBits)
    {
        var protector = CreateProtector();

        var exception = Assert.Throws<ArgumentException>(() =>
            _generator.GenerateSecret(protector, CreateEccRequest(keySizeBits, "P-384"))
        );

        Assert.StartsWith(
            $"The ECC key size '{keySizeBits}' bits is invalid for the curve 'P-384', whose keys are 384 bits.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region Envelope Tests

    [Fact]
    public void GenerateSecret_ProducesEnvelope()
    {
        var protector = CreateProtector();

        var persisted = _generator
            .GenerateSecret(protector, CreateRequest(SecretTypes.Symmetric, 256))
            .Secret;

        Assert.StartsWith(
            SecretProtectionFormat.Prefix,
            persisted.EncodedValue,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(SecretTypes.Symmetric, 256, typeof(SymmetricSecretKey))]
    [InlineData(SecretTypes.Rsa, 2048, typeof(RsaSecretKey))]
    public void GenerateSecret_RoundTripsThroughSerializer(
        string secretType,
        int keySizeBits,
        Type expectedKeyType
    )
    {
        var protector = CreateProtector();

        var persisted = _generator
            .GenerateSecret(protector, CreateRequest(secretType, keySizeBits))
            .Secret;

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        Assert.IsAssignableFrom(expectedKeyType, secretKey);
        Assert.Equal("gen-1", secretKey.KeyId);
        Assert.Equal(keySizeBits, secretKey.KeySizeBits);
    }

    [Fact]
    public async Task GenerateSharedSecret_StoresOnlyTheSha256VerifierOfTheRevealedSecret()
    {
        var protector = CreateProtector();

        var generated = _generator.GenerateSharedSecretVerifier(protector, CreateSharedRequest());
        var persisted = generated.Secret;

        Assert.StartsWith(
            SecretProtectionFormat.Prefix,
            persisted.EncodedValue,
            StringComparison.Ordinal
        );
        Assert.Equal(SecretTypes.Symmetric, persisted.SecretType);
        Assert.Equal(SecretEncodings.Verifier, persisted.SecretEncoding);
        Assert.Null(persisted.PublicKey);
        Assert.NotNull(generated.SecretMaterial);

        var expected = SecureEncoding.UTF8.GetBytes(generated.SecretMaterial);
        Assert.Equal(expected.Length * 8, persisted.KeySizeBits);

        // The stored text is the digest of the revealed secret's UTF-8 bytes, never the secret itself.
        var text = ReadVerifierText(protector, persisted);
        Assert.Equal(
            "$ncode-sha256$"
                + System.Buffers.Text.Base64Url.EncodeToString(SHA256.HashData(expected)),
            text
        );
        Assert.DoesNotContain(generated.SecretMaterial, text, StringComparison.Ordinal);

        var verifier = ReadVerifier(protector, persisted);
        Assert.Equal("gen-1", verifier.KeyId);
        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync(expected, CancellationToken.None)
        );
        Assert.Equal(
            SecretVerificationResult.Failed,
            await verifier.VerifyAsync(
                SecureEncoding.UTF8.GetBytes(generated.SecretMaterial + "x"),
                CancellationToken.None
            )
        );

        // A verifier is not key material.
        Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(protector, persisted)
        );
    }

    [Fact]
    public void GenerateReversibleSharedSecret_StoresUtf8OfRevealedSecret()
    {
        var protector = CreateProtector();

        var generated = _generator.GenerateReversibleSharedSecret(protector, CreateSharedRequest());
        var persisted = generated.Secret;

        Assert.StartsWith(
            SecretProtectionFormat.Prefix,
            persisted.EncodedValue,
            StringComparison.Ordinal
        );
        Assert.Equal(SecretTypes.Symmetric, persisted.SecretType);
        Assert.Equal(SecretEncodings.SymmetricRaw, persisted.SecretEncoding);
        Assert.NotNull(generated.SecretMaterial);
        Assert.NotEmpty(generated.SecretMaterial);

        var secretKey = Assert.IsAssignableFrom<SymmetricSecretKey>(
            _serializer.DeserializeSecret(protector, persisted)
        );

        var writer = new ArrayBufferWriter<byte>();
        secretKey.ExportPrivateKey(ref writer);

        // The core contract (ADR-1110): client_secret_jwt keys its HMAC from the UTF-8 bytes of the presented secret, so
        // the stored material must be exactly the UTF-8 bytes of the revealed opaque string.
        var expected = SecureEncoding.UTF8.GetBytes(generated.SecretMaterial);
        Assert.Equal(expected, writer.WrittenSpan.ToArray());
        Assert.Equal(expected.Length * 8, persisted.KeySizeBits);
    }

    [Fact]
    public async Task ImportSharedSecret_StoresOnlyThePbkdf2VerifierOfTheGivenSecret()
    {
        var protector = CreateProtector();
        var secretValue = "my-chosen-client-secret"u8.ToArray();

        var persisted = _generator.ImportSharedSecretVerifier(
            protector,
            CreateImportRequest("import-1"),
            secretValue
        );

        Assert.StartsWith(
            SecretProtectionFormat.Prefix,
            persisted.EncodedValue,
            StringComparison.Ordinal
        );
        Assert.Equal("import-1", persisted.SecretId);
        Assert.Equal(SecretTypes.Symmetric, persisted.SecretType);
        Assert.Equal(SecretEncodings.Verifier, persisted.SecretEncoding);
        Assert.Equal(secretValue.Length * 8, persisted.KeySizeBits);

        // The text records the scheme and the configured work factor, and a fresh salt.
        var text = ReadVerifierText(protector, persisted);
        Assert.StartsWith(
            $"$ncode-pbkdf2-sha256${TestSecretVerifierSchemes.Iterations}$",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("my-chosen-client-secret", text, StringComparison.Ordinal);
        var again = _generator.ImportSharedSecretVerifier(
            protector,
            CreateImportRequest("import-1"),
            secretValue
        );
        Assert.NotEqual(text, ReadVerifierText(protector, again));

        var verifier = ReadVerifier(protector, persisted);
        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, verifier.Scheme);
        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync(secretValue, CancellationToken.None)
        );
        Assert.Equal(
            SecretVerificationResult.Failed,
            await verifier.VerifyAsync(
                "my-chosen-client-secreT"u8.ToArray(),
                CancellationToken.None
            )
        );
        Assert.Equal(
            SecretVerificationResult.Failed,
            await verifier.VerifyAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)
        );
    }

    [Fact]
    public void ImportReversibleSharedSecret_StoresTheGivenUtf8BytesVerbatim()
    {
        var protector = CreateProtector();
        var secretValue = "my-chosen-client-secret"u8.ToArray();

        var persisted = _generator.ImportReversibleSharedSecret(
            protector,
            CreateImportRequest("import-1"),
            secretValue
        );

        Assert.StartsWith(
            SecretProtectionFormat.Prefix,
            persisted.EncodedValue,
            StringComparison.Ordinal
        );
        Assert.Equal("import-1", persisted.SecretId);
        Assert.Equal(SecretTypes.Symmetric, persisted.SecretType);
        Assert.Equal(SecretEncodings.SymmetricRaw, persisted.SecretEncoding);
        Assert.Equal(secretValue.Length * 8, persisted.KeySizeBits);

        var secretKey = Assert.IsAssignableFrom<SymmetricSecretKey>(
            _serializer.DeserializeSecret(protector, persisted)
        );

        var writer = new ArrayBufferWriter<byte>();
        secretKey.ExportPrivateKey(ref writer);

        Assert.Equal(secretValue, writer.WrittenSpan.ToArray());
    }

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void ImportSharedSecret_WhenValueEmpty_Throws(bool reversible)
    {
        var protector = CreateProtector();
        var request = CreateImportRequest("import-2");

        Assert.Throws<ArgumentException>(() =>
            reversible
                ? _generator.ImportReversibleSharedSecret(
                    protector,
                    request,
                    ReadOnlySpan<byte>.Empty
                )
                : _generator.ImportSharedSecretVerifier(
                    protector,
                    request,
                    ReadOnlySpan<byte>.Empty
                )
        );
    }

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void ImportSharedSecret_RecordsThatACallerSuppliedIt(bool reversible)
    {
        var protector = CreateProtector();
        var request = CreateImportRequest("import-7");
        var secretValue = "my-chosen-client-secret"u8.ToArray();

        var imported = reversible
            ? _generator.ImportReversibleSharedSecret(protector, request, secretValue)
            : _generator.ImportSharedSecretVerifier(protector, request, secretValue);

        Assert.Equal(SecretOrigins.Supplied, imported.Origin);
    }

    #endregion

    #region ConvertToSharedSecretVerifier Tests

    [Fact]
    public async Task ConvertToSharedSecretVerifier_WhenTheServerGeneratedTheSecret_StoresItsSha256Verifier()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateReversibleSharedSecret(protector, CreateSharedRequest());
        var secretValue = SecureEncoding.UTF8.GetBytes(generated.SecretMaterial!);

        var converted = _generator.ConvertToSharedSecretVerifier(
            protector,
            generated.Secret,
            "converted-1"
        );

        Assert.Equal("converted-1", converted.SecretId);
        Assert.Equal(SecretTypes.Symmetric, converted.SecretType);
        Assert.Equal(SecretEncodings.Verifier, converted.SecretEncoding);
        Assert.Equal(SecretOrigins.Generated, converted.Origin);
        Assert.Equal(generated.Secret.KeySizeBits, converted.KeySizeBits);
        Assert.Equal(generated.Secret.Use, converted.Use);
        Assert.Equal(generated.Secret.Algorithm, converted.Algorithm);
        Assert.Equal(generated.Secret.CreatedWhen, converted.CreatedWhen);
        Assert.Equal(generated.Secret.ExpiresWhen, converted.ExpiresWhen);

        var verifier = ReadVerifier(protector, converted);
        Assert.Equal(SecretVerifierSchemes.Sha256, verifier.Scheme);
        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync(secretValue, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ConvertToSharedSecretVerifier_WhenACallerSuppliedTheSecret_StoresItsPbkdf2Verifier()
    {
        var protector = CreateProtector();
        var secretValue = "my-chosen-client-secret"u8.ToArray();
        var imported = _generator.ImportReversibleSharedSecret(
            protector,
            CreateImportRequest("import-8"),
            secretValue
        );

        var converted = _generator.ConvertToSharedSecretVerifier(
            protector,
            imported,
            "converted-2"
        );

        Assert.Equal(SecretOrigins.Supplied, converted.Origin);
        Assert.Equal(secretValue.Length * 8, converted.KeySizeBits);
        Assert.DoesNotContain(
            "my-chosen-client-secret",
            ReadVerifierText(protector, converted),
            StringComparison.Ordinal
        );

        var verifier = ReadVerifier(protector, converted);
        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, verifier.Scheme);
        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync(secretValue, CancellationToken.None)
        );
    }

    [Fact]
    public void ConvertToSharedSecretVerifier_WhenTheSecretIsAVerifier_Throws()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateSharedSecretVerifier(protector, CreateSharedRequest());

        Assert.Throws<InvalidOperationException>(() =>
            _generator.ConvertToSharedSecretVerifier(protector, generated.Secret, "converted-3")
        );
    }

    [Fact]
    public void ConvertToSharedSecretVerifier_WhenTheSecretIsNotShared_Throws()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateSecret(protector, CreateRequest(SecretTypes.Rsa, 2048));

        Assert.Throws<InvalidOperationException>(() =>
            _generator.ConvertToSharedSecretVerifier(protector, generated.Secret, "converted-4")
        );
    }

    #endregion

    #region GenerateRemoteSecretAsync Tests

    private static byte[] CreateRsaSubjectPublicKeyInfo()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportSubjectPublicKeyInfo();
    }

    private static byte[] CreateEccSubjectPublicKeyInfo()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportSubjectPublicKeyInfo();
    }

    private static GenerateRemoteSecretRequest CreateRemoteRequest(
        string secretType = SecretTypes.Rsa,
        int keySizeBits = 2048,
        string? curve = null,
        string algorithm = "RS256",
        string provider = RemoteKeyProviderNames.AwsKms,
        DateTimeOffset? activatesWhen = null,
        DateTimeOffset? retiresWhen = null,
        string? managedBy = null
    ) =>
        new()
        {
            SecretId = "remote-1",
            Owner = TenantOwner,
            Provider = provider,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
            Algorithm = algorithm,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            ManagedBy = managedBy,
        };

    private Moq.Language.Flow.ISetup<
        IRemoteKeyProvider,
        ValueTask<RemoteKeyCreationResult>
    > SetupCreateKeyCall(RemoteKeyAlgorithm row) =>
        _remoteProvider.Setup(x =>
            x.CreateKeyAsync(
                It.Is<RemoteKeyCreationRequest>(request =>
                    request.OwnerId == "tenant:tenant-one"
                    && request.SecretId == "remote-1"
                    && ReferenceEquals(request.Algorithm, row)
                    && request.GroupName
                        == RemoteKeyGroupName.Create("test", "tenant:tenant-one", row)
                    && request.Deployment == "test"
                ),
                CancellationToken.None
            )
        );

    private void SetupCreateKey(RemoteKeyAlgorithm row) =>
        SetupCreateKeyCall(row)
            .ReturnsAsync(new RemoteKeyCreationResult { KeyId = "key-1" })
            .Verifiable();

    private void SetupGetPublicKey(RemoteKeyAlgorithm row, byte[] subjectPublicKeyInfo) =>
        SetupGetPublicKeyCall(row)
            .Callback(
                (RemotePublicKeyRequest _, IBufferWriter<byte> writer, CancellationToken _) =>
                    writer.Write(subjectPublicKeyInfo)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private Moq.Language.Flow.ISetup<IRemoteKeyProvider, ValueTask> SetupGetPublicKeyCall(
        RemoteKeyAlgorithm row
    ) =>
        _remoteProvider.Setup(x =>
            x.GetPublicKeyAsync(
                It.Is<RemotePublicKeyRequest>(request =>
                    request.KeyId == "key-1" && ReferenceEquals(request.Algorithm, row)
                ),
                It.IsAny<IBufferWriter<byte>>(),
                CancellationToken.None
            )
        );

    private void SetupCreateAndReadKey(RemoteKeyAlgorithm row, byte[] subjectPublicKeyInfo)
    {
        SetupCreateKey(row);
        SetupGetPublicKey(row, subjectPublicKeyInfo);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenRsa_CallsTheProviderWithTheRowAndStoresTheRemoteRecord()
    {
        var spki = CreateRsaSubjectPublicKeyInfo();
        SetupCreateAndReadKey(RsaRow, spki);

        var secret = await _generator.GenerateRemoteSecretAsync(
            CreateRemoteRequest(),
            CancellationToken.None
        );

        Assert.Equal("remote-1", secret.SecretId);
        Assert.Equal(SecretTypes.Rsa, secret.SecretType);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal("aws-kms:key-1", secret.EncodedValue);
        Assert.Equal(Base64Url.Encode(spki), secret.PublicKey);
        Assert.Equal(2048, secret.KeySizeBits);
        Assert.Equal("RS256", secret.Algorithm);
        Assert.Equal("sig", secret.Use);
        Assert.Equal(SecretOrigins.Generated, secret.Origin);
        Assert.Equal(DateTimeOffset.UnixEpoch, secret.CreatedWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch, secret.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddYears(1), secret.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddYears(1), secret.ExpiresWhen);
        Assert.Null(secret.ManagedBy);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenEcc_StoresTheCurveKeySize()
    {
        var spki = CreateEccSubjectPublicKeyInfo();
        SetupCreateAndReadKey(EccRow, spki);

        var secret = await _generator.GenerateRemoteSecretAsync(
            CreateRemoteRequest(SecretTypes.Ecc, 256, "P-256", "ES256"),
            CancellationToken.None
        );

        Assert.Equal(SecretTypes.Ecc, secret.SecretType);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(256, secret.KeySizeBits);
        Assert.Equal(Base64Url.Encode(spki), secret.PublicKey);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenTheRequestNamesALifecycleAndManager_StoresThem()
    {
        SetupCreateAndReadKey(RsaRow, CreateRsaSubjectPublicKeyInfo());
        var request = CreateRemoteRequest(
            activatesWhen: DateTimeOffset.UnixEpoch.AddDays(1),
            retiresWhen: DateTimeOffset.UnixEpoch.AddDays(30),
            managedBy: "rollover"
        );

        var secret = await _generator.GenerateRemoteSecretAsync(request, CancellationToken.None);

        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(1), secret.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(30), secret.RetiresWhen);
        Assert.Equal("rollover", secret.ManagedBy);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData(SecretTypes.Symmetric)]
    public async Task GenerateRemoteSecretAsync_WhenTheSecretTypeCannotBeRemote_ThrowsBeforeAnyProviderCall(
        string secretType
    )
    {
        var request = CreateRemoteRequest(secretType);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.GenerateRemoteSecretAsync(request, CancellationToken.None)
        );

        Assert.Contains($"'{secretType}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenTheProviderIsNotRegistered_ThrowsBeforeAnyProviderCall()
    {
        var request = CreateRemoteRequest(provider: "unregistered-kms");

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.GenerateRemoteSecretAsync(request, CancellationToken.None)
        );

        Assert.Contains("'unregistered-kms'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenNoRowMatches_ThrowsBeforeAnyProviderCall()
    {
        var request = CreateRemoteRequest(algorithm: "RS384");

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.GenerateRemoteSecretAsync(request, CancellationToken.None)
        );

        Assert.Contains("'RS384'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_DecryptionRow_StoresAnEncryptionSecret()
    {
        var spki = CreateRsaSubjectPublicKeyInfo();
        SetupCreateAndReadKey(OaepRow, spki);

        var secret = await _generator.GenerateRemoteSecretAsync(
            CreateRemoteRequest(algorithm: "RSA-OAEP-256"),
            CancellationToken.None
        );

        Assert.Equal(SecretKeyUses.Encryption, secret.Use);
        Assert.Equal("RSA-OAEP-256", secret.Algorithm);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(Base64Url.Encode(spki), secret.PublicKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GenerateRemoteSecretAsync_DeploymentUnset_ThrowsBeforeCreating(
        string? deployment
    )
    {
        var generator = CreateGenerator(new SharedSecretVerifierOptions(), deployment);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await generator.GenerateRemoteSecretAsync(CreateRemoteRequest(), CancellationToken.None)
        );

        Assert.Equal("SecretProtection:RemoteKeys:Deployment is not set.", exception.Message);
    }

    // Expects one call to the destroyer with the one record the generator would have returned for the created key.
    private void SetupDestroy() =>
        _remoteDestroyer
            .Setup(x =>
                x.DestroyAsync(
                    It.Is<IReadOnlyCollection<PersistedSecret>>(secrets =>
                        secrets.Count == 1
                        && secrets.Single().EncodedValue == "aws-kms:key-1"
                        && secrets.Single().SecretEncoding == SecretEncodings.Remote
                        && secrets.Single().Origin == SecretOrigins.Generated
                    )
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenTheKeySizeDiffersFromTheRow_HandsTheRecordToTheDestroyerAndThrows()
    {
        SetupCreateAndReadKey(LargeRsaRow, CreateRsaSubjectPublicKeyInfo());
        SetupDestroy();

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.GenerateRemoteSecretAsync(
                CreateRemoteRequest(keySizeBits: 4096),
                CancellationToken.None
            )
        );

        Assert.Contains("key size 4096", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenTheCurveDiffersFromTheRow_HandsTheRecordToTheDestroyerAndThrows()
    {
        SetupCreateAndReadKey(OtherCurveEccRow, CreateEccSubjectPublicKeyInfo());
        SetupDestroy();

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.GenerateRemoteSecretAsync(
                CreateRemoteRequest(SecretTypes.Ecc, 256, "secp256k1", "ES256"),
                CancellationToken.None
            )
        );

        Assert.Contains("curve 'secp256k1'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenThePublicKeyIsNotValidDer_HandsTheRecordToTheDestroyerAndThrowsWithTheReadFailure()
    {
        SetupCreateAndReadKey(RsaRow, [1, 2, 3]);
        SetupDestroy();

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.GenerateRemoteSecretAsync(
                CreateRemoteRequest(),
                CancellationToken.None
            )
        );

        Assert.Contains($"'{SecretTypes.Rsa}' key", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenThePublicKeyIsOfAnotherKeyFamily_HandsTheRecordToTheDestroyerAndThrowsWithTheReadFailure()
    {
        SetupCreateAndReadKey(RsaRow, CreateEccSubjectPublicKeyInfo());
        SetupDestroy();

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.GenerateRemoteSecretAsync(
                CreateRemoteRequest(),
                CancellationToken.None
            )
        );

        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenReadingThePublicKeyFails_HandsTheCreatedKeyToTheDestroyerAndThrows()
    {
        SetupCreateKey(RsaRow);
        SetupGetPublicKeyCall(RsaRow).ThrowsAsync(new IOException()).Verifiable();
        SetupDestroy();

        await Assert.ThrowsAsync<IOException>(async () =>
            await _generator.GenerateRemoteSecretAsync(
                CreateRemoteRequest(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenCancelledWhileReadingThePublicKey_HandsTheCreatedKeyToTheDestroyerAndThrows()
    {
        SetupCreateKey(RsaRow);
        SetupGetPublicKeyCall(RsaRow).ThrowsAsync(new OperationCanceledException()).Verifiable();
        SetupDestroy();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _generator.GenerateRemoteSecretAsync(
                CreateRemoteRequest(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task GenerateRemoteSecretAsync_WhenCreationFails_DestroysNothing()
    {
        SetupCreateKeyCall(RsaRow).ThrowsAsync(new IOException()).Verifiable();
        _remoteDestroyer
            .Setup(x => x.DestroyAsync(It.IsAny<IReadOnlyCollection<PersistedSecret>>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Never());

        await Assert.ThrowsAsync<IOException>(async () =>
            await _generator.GenerateRemoteSecretAsync(
                CreateRemoteRequest(),
                CancellationToken.None
            )
        );
    }

    #endregion

    #region BindRemoteSecretAsync Tests

    private static BindRemoteSecretRequest CreateBindRequest(
        string secretType = SecretTypes.Rsa,
        int keySizeBits = 2048,
        string? curve = null,
        string algorithm = "RS256",
        string provider = RemoteKeyProviderNames.AwsKms
    ) =>
        new()
        {
            SecretId = "remote-1",
            Provider = provider,
            KeyId = "key-1",
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
            Algorithm = algorithm,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    [Fact]
    public async Task BindRemoteSecretAsync_WhenRsa_ReadsThePublicKeyWithTheRowAndStoresASuppliedRemoteRecord()
    {
        var spki = CreateRsaSubjectPublicKeyInfo();
        SetupGetPublicKey(RsaRow, spki);

        var secret = await _generator.BindRemoteSecretAsync(
            CreateBindRequest(),
            CancellationToken.None
        );

        Assert.Equal("remote-1", secret.SecretId);
        Assert.Equal(SecretTypes.Rsa, secret.SecretType);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal("aws-kms:key-1", secret.EncodedValue);
        Assert.Equal(Base64Url.Encode(spki), secret.PublicKey);
        Assert.Equal(2048, secret.KeySizeBits);
        Assert.Equal("RS256", secret.Algorithm);
        Assert.Equal("sig", secret.Use);
        Assert.Equal(SecretOrigins.Supplied, secret.Origin);
        Assert.Null(secret.ManagedBy);
        Assert.Equal(DateTimeOffset.UnixEpoch, secret.CreatedWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch, secret.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddYears(1), secret.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddYears(1), secret.ExpiresWhen);
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenEcc_StoresTheCurveKeySize()
    {
        var spki = CreateEccSubjectPublicKeyInfo();
        SetupGetPublicKey(EccRow, spki);

        var secret = await _generator.BindRemoteSecretAsync(
            CreateBindRequest(SecretTypes.Ecc, 256, "P-256", "ES256"),
            CancellationToken.None
        );

        Assert.Equal(SecretTypes.Ecc, secret.SecretType);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(256, secret.KeySizeBits);
        Assert.Equal(Base64Url.Encode(spki), secret.PublicKey);
    }

    [Fact]
    public async Task BindRemoteSecretAsync_DecryptionRow_StoresAnEncryptionSecret()
    {
        var spki = CreateRsaSubjectPublicKeyInfo();
        SetupGetPublicKey(OaepRow, spki);

        var secret = await _generator.BindRemoteSecretAsync(
            CreateBindRequest(algorithm: "RSA-OAEP-256"),
            CancellationToken.None
        );

        Assert.Equal(SecretKeyUses.Encryption, secret.Use);
        Assert.Equal("RSA-OAEP-256", secret.Algorithm);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(Base64Url.Encode(spki), secret.PublicKey);
        Assert.Equal(SecretOrigins.Supplied, secret.Origin);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData(SecretTypes.Symmetric)]
    public async Task BindRemoteSecretAsync_WhenTheSecretTypeCannotBeRemote_ThrowsWithoutCallingTheProvider(
        string secretType
    )
    {
        var request = CreateBindRequest(secretType);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.BindRemoteSecretAsync(request, CancellationToken.None)
        );

        Assert.Contains($"'{secretType}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenTheProviderIsNotRegistered_ThrowsWithoutCallingTheProvider()
    {
        var request = CreateBindRequest(provider: "unregistered-kms");

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.BindRemoteSecretAsync(request, CancellationToken.None)
        );

        Assert.Contains("'unregistered-kms'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenNoRowMatches_ThrowsWithoutCallingTheProvider()
    {
        var request = CreateBindRequest(algorithm: "RS384");

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.BindRemoteSecretAsync(request, CancellationToken.None)
        );

        Assert.Contains("'RS384'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenTheKeySizeDiffersFromTheRow_Throws()
    {
        SetupGetPublicKey(LargeRsaRow, CreateRsaSubjectPublicKeyInfo());
        var request = CreateBindRequest(keySizeBits: LargeRsaRow.KeySizeBits);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.BindRemoteSecretAsync(request, CancellationToken.None)
        );

        Assert.Contains("'key-1'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenThePublicKeyIsNotValidDer_ThrowsWithTheReadFailure()
    {
        SetupGetPublicKey(RsaRow, [1, 2, 3]);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await _generator.BindRemoteSecretAsync(CreateBindRequest(), CancellationToken.None)
        );

        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenTheProviderRefusesTheKey_PropagatesItsException()
    {
        SetupGetPublicKeyCall(RsaRow)
            .ThrowsAsync(new CryptographicException("refused"))
            .Verifiable();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await _generator.BindRemoteSecretAsync(CreateBindRequest(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenTheProviderFailsWithAnInvalidOperation_PropagatesIt()
    {
        SetupGetPublicKeyCall(RsaRow)
            .ThrowsAsync(new InvalidOperationException("The credentials are not available."))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _generator.BindRemoteSecretAsync(CreateBindRequest(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task BindRemoteSecretAsync_WhenTheKeyIdIsEmpty_ThrowsWithoutCallingTheProvider()
    {
        var request = new BindRemoteSecretRequest
        {
            SecretId = "remote-1",
            Provider = RemoteKeyProviderNames.AwsKms,
            KeyId = "",
            SecretType = SecretTypes.Rsa,
            KeySizeBits = 2048,
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _generator.BindRemoteSecretAsync(request, CancellationToken.None)
        );
    }

    #endregion
}
