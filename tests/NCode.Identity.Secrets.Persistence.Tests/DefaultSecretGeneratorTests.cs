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
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretGeneratorTests
{
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("tenant-one");

    // A low work factor keeps the tests fast; a verifier records its own iteration count.
    private const int TestIterations = 1_000;

    private readonly DefaultSecretKeyTypeCatalog _catalog;

    private readonly DefaultSecretGenerator _generator;
    private readonly DefaultSecretSerializer _serializer;

    public DefaultSecretGeneratorTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        var factoryProtector = provider.CreateProtector("factory");

        var secretKeyFactory = new DefaultSecretKeyFactory(
            new TestDataProtectorFactory<SecretKey>(factoryProtector)
        );

        var catalog = new DefaultSecretKeyTypeCatalog([
            new SymmetricSecretKeyTypeDescriptor(secretKeyFactory),
            new RsaSecretKeyTypeDescriptor(secretKeyFactory),
            new EccSecretKeyTypeDescriptor(secretKeyFactory, new DefaultEccCurveCatalog([])),
        ]);

        _catalog = catalog;
        _generator = CreateGenerator(TestIterations);
        _serializer = new DefaultSecretSerializer(catalog);
    }

    private DefaultSecretGenerator CreateGenerator(int iterations) =>
        new(
            _catalog,
            Microsoft.Extensions.Options.Options.Create(
                new SharedSecretVerifierOptions { Pbkdf2Iterations = iterations }
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
            KeySizeBits = keySizeBits,
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

    private static ImportSharedSecretRequest CreateImportRequest(string secretId) =>
        new()
        {
            SecretId = secretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    #region GenerateSecret Symmetric Tests

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

    #region GenerateSharedSecret Tests

    public static TheoryData<bool> BothSharedSecretForms => new() { false, true };

    private GeneratedSecret GenerateSharedSecret(
        DefaultSecretProtector protector,
        GenerateSecretRequest request,
        bool reversible
    ) =>
        reversible
            ? _generator.GenerateReversibleSharedSecret(protector, request)
            : _generator.GenerateSharedSecret(protector, request);

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void GenerateSharedSecret_WhenNotSymmetric_Throws(bool reversible)
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Rsa, 2048);

        Assert.Throws<InvalidOperationException>(() =>
            GenerateSharedSecret(protector, request, reversible)
        );
    }

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void GenerateSharedSecret_WhenSizeNotMultipleOfEight_Throws(bool reversible)
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 100);

        Assert.Throws<ArgumentException>(() =>
            GenerateSharedSecret(protector, request, reversible)
        );
    }

    [Theory]
    [MemberData(nameof(BothSharedSecretForms))]
    public void GenerateSharedSecret_WhenEntropyIsBelowTheMinimum_Throws(bool reversible)
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 128);

        Assert.Throws<ArgumentException>(() =>
            GenerateSharedSecret(protector, request, reversible)
        );
    }

    [Fact]
    public void SharedSecretVerifierOptions_DefaultsToTheIterationsOfThePasswordHashes()
    {
        // OWASP's recommendation for PBKDF2-HMAC-SHA256, as the family's password hashes use.
        Assert.Equal(600_000, new SharedSecretVerifierOptions().Pbkdf2Iterations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenIterationsAreBelowOne_Throws(int iterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateGenerator(iterations));
    }

    #endregion

    #region ImportSharedSecret Tests

    [Fact]
    public void ImportSharedSecret_StoresAVerifierOfTheSameShapeAsGenerateSharedSecret()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateSharedSecret(
            protector,
            CreateRequest(SecretTypes.Symmetric, 256)
        );
        var secretValue = SecureEncoding.UTF8.GetBytes(generated.SecretMaterial!);

        var imported = _generator.ImportSharedSecret(
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
        Assert.True(generatedVerifier.Verify(secretValue));
        Assert.True(importedVerifier.Verify(secretValue));
    }

    [Fact]
    public void ImportReversibleSharedSecret_ProducesTheSameStoredShapeAsGenerateReversibleSharedSecret()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateReversibleSharedSecret(
            protector,
            CreateRequest(SecretTypes.Symmetric, 256)
        );

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

        var generated = _generator.GenerateSharedSecret(
            protector,
            CreateRequest(SecretTypes.Symmetric, 256)
        );

        Assert.Null(generated.Secret.PublicKey);
    }

    [Fact]
    public void GenerateSecret_WhenEccWithCurveName_UsesThatCurveSize()
    {
        var protector = CreateProtector();
        var request = new GenerateSecretRequest
        {
            SecretId = "gen-ecc",
            SecretType = SecretTypes.Ecc,
            KeySizeBits = 0,
            Curve = "P-384",
            Use = "sig",
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

        var persisted = _generator.GenerateSecret(protector, request).Secret;

        Assert.Equal(384, persisted.KeySizeBits);
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
    public void GenerateSharedSecret_StoresOnlyTheSha256VerifierOfTheRevealedSecret()
    {
        var protector = CreateProtector();

        var generated = _generator.GenerateSharedSecret(
            protector,
            CreateRequest(SecretTypes.Symmetric, 256)
        );
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
        Assert.True(verifier.Verify(expected));
        Assert.False(verifier.Verify(SecureEncoding.UTF8.GetBytes(generated.SecretMaterial + "x")));

        // A verifier is not key material.
        Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(protector, persisted)
        );
    }

    [Fact]
    public void GenerateReversibleSharedSecret_StoresUtf8OfRevealedSecret()
    {
        var protector = CreateProtector();

        var generated = _generator.GenerateReversibleSharedSecret(
            protector,
            CreateRequest(SecretTypes.Symmetric, 256)
        );
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
    public void ImportSharedSecret_StoresOnlyThePbkdf2VerifierOfTheGivenSecret()
    {
        var protector = CreateProtector();
        var secretValue = SecureEncoding.UTF8.GetBytes("my-chosen-client-secret");

        var persisted = _generator.ImportSharedSecret(
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
            $"$ncode-pbkdf2-sha256${TestIterations}$",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("my-chosen-client-secret", text, StringComparison.Ordinal);
        var again = _generator.ImportSharedSecret(
            protector,
            CreateImportRequest("import-1"),
            secretValue
        );
        Assert.NotEqual(text, ReadVerifierText(protector, again));

        var verifier = ReadVerifier(protector, persisted);
        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, verifier.Scheme);
        Assert.True(verifier.Verify(secretValue));
        Assert.False(verifier.Verify(SecureEncoding.UTF8.GetBytes("my-chosen-client-secreT")));
        Assert.False(verifier.Verify([]));
    }

    [Fact]
    public void ImportReversibleSharedSecret_StoresTheGivenUtf8BytesVerbatim()
    {
        var protector = CreateProtector();
        var secretValue = SecureEncoding.UTF8.GetBytes("my-chosen-client-secret");

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
                : _generator.ImportSharedSecret(protector, request, ReadOnlySpan<byte>.Empty)
        );
    }

    #endregion
}
