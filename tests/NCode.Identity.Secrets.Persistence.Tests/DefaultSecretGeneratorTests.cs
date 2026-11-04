// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretGeneratorTests
{
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("tenant-one");

    private readonly DefaultSecretKeyTypeCatalog _catalog;
    private readonly DefaultSecretVerifierSchemeCatalog _verifierCatalog;

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
        _generator = CreateGenerator(new SharedSecretVerifierOptions());
        _serializer = new DefaultSecretSerializer(catalog, _verifierCatalog);
    }

    private DefaultSecretGenerator CreateGenerator(SharedSecretVerifierOptions options) =>
        new(_catalog, _verifierCatalog, Microsoft.Extensions.Options.Options.Create(options));

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
}
