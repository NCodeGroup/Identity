// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretSerializerTests : BaseTests
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

    private readonly DefaultSecretKeyTypeCatalog _catalog;
    private readonly DefaultRemoteKeyProviderCatalog _remoteCatalog;
    private readonly DefaultRemoteKeyDescriptorCatalog _remoteDescriptorCatalog;
    private readonly DefaultSecretSerializer _serializer;

    public DefaultSecretSerializerTests()
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
        _remoteDescriptorCatalog = CreateRemoteDescriptorCatalog();
        _remoteCatalog = CreateRemoteCatalog(_remoteDescriptorCatalog);
        _serializer = new DefaultSecretSerializer(
            catalog,
            TestSecretVerifierSchemes.CreateCatalog(),
            _remoteCatalog,
            _remoteDescriptorCatalog
        );
    }

    private DefaultRemoteKeyDescriptorCatalog CreateRemoteDescriptorCatalog()
    {
        var source = CreateStrictMock<IRemoteKeyDescriptorSource>();
        source
            .SetupGet(x => x.Providers)
            .Returns([new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }])
            .Verifiable();
        source.SetupGet(x => x.Algorithms).Returns([RsaRow, EccRow]).Verifiable();

        return new DefaultRemoteKeyDescriptorCatalog([source.Object]);
    }

    private DefaultRemoteKeyProviderCatalog CreateRemoteCatalog(
        DefaultRemoteKeyDescriptorCatalog descriptorCatalog
    )
    {
        var provider = CreateStrictMock<IRemoteKeyProvider>();
        provider.SetupGet(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();

        return new DefaultRemoteKeyProviderCatalog([provider.Object], descriptorCatalog);
    }

    private static PersistedSecret CreateRemoteSecret(
        string secretType,
        int keySizeBits,
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        string? algorithm,
        string reference = "aws-kms:key-1"
    ) =>
        CreatePersistedSecretFromEncodedValue(
            secretType,
            SecretEncodings.Remote,
            keySizeBits,
            reference,
            publicKey: subjectPublicKeyInfo.IsEmpty
                ? null
                : Base64Url.EncodeToString(subjectPublicKeyInfo),
            algorithm: algorithm
        );

    private static PersistedSecret CreatePersistedSecret(
        ISecretProtector protector,
        string secretType,
        int keySizeBits,
        ReadOnlySpan<byte> rawBytes,
        string secretId = "secret-1",
        DateTimeOffset? expiresWhen = null,
        string? keyId = null,
        DateTimeOffset? activatesWhen = null,
        DateTimeOffset? retiresWhen = null
    ) =>
        CreatePersistedSecretFromEncodedValue(
            secretType,
            secretType switch
            {
                SecretTypes.Symmetric => SecretEncodings.SymmetricRaw,
                _ => SecretEncodings.Pkcs8,
            },
            keySizeBits,
            CreateEnvelope(protector, secretId, rawBytes),
            secretId,
            expiresWhen,
            keyId,
            activatesWhen: activatesWhen,
            retiresWhen: retiresWhen
        );

    private static PersistedSecret CreatePersistedSecretFromEncodedValue(
        string secretType,
        string secretEncoding,
        int keySizeBits,
        string encodedValue,
        string secretId = "secret-1",
        DateTimeOffset? expiresWhen = null,
        string? keyId = null,
        string? publicKey = null,
        DateTimeOffset? activatesWhen = null,
        DateTimeOffset? retiresWhen = null,
        string? algorithm = null
    ) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            KeyId = keyId,
            Use = "sig",
            Algorithm = algorithm,
            ActivatesWhen = activatesWhen ?? DateTimeOffset.UnixEpoch,
            RetiresWhen = retiresWhen ?? expiresWhen ?? DateTimeOffset.UnixEpoch.AddYears(1),
            ExpiresWhen = expiresWhen ?? DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = secretType,
            SecretEncoding = secretEncoding,
            KeySizeBits = keySizeBits,
            EncodedValue = encodedValue,
            PublicKey = publicKey,
        }.Build();

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

    private static string CreateEnvelope(
        ISecretProtector protector,
        string secretId,
        ReadOnlySpan<byte> rawBytes
    ) => protector.ProtectToString(rawBytes, SecretProtectionContext.ForPersistedSecret(secretId));

    private static PersistedSecret CreateVerifierSecret(
        ISecretProtector protector,
        string verifierText,
        string secretId = "secret-1",
        string? keyId = null
    ) =>
        CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.Verifier,
            256,
            CreateEnvelope(protector, secretId, SecureEncoding.ASCII.GetBytes(verifierText)),
            secretId,
            keyId: keyId
        );

    private static string Sha256Text(string secret) =>
        "$ncode-sha256$"
        + Base64Url.EncodeToString(SHA256.HashData(SecureEncoding.UTF8.GetBytes(secret)));

    #region DeserializeSecret KeyId Tests

    [Fact]
    public void DeserializeSecret_WhenKeyIdIsSet_SelectsKeyByKeyId()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var protector = CreateProtector();
        var persisted = CreatePersistedSecret(
            protector,
            SecretTypes.Symmetric,
            keyMaterial.Length * 8,
            keyMaterial,
            keyId: "client-kid"
        );

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        Assert.Equal("client-kid", secretKey.KeyId);
    }

    [Fact]
    public void DeserializeSecret_CarriesTheInstantsOfTheLifecycleOnTheKeysMetadata()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var protector = CreateProtector();
        var persisted = CreatePersistedSecret(
            protector,
            SecretTypes.Symmetric,
            keyMaterial.Length * 8,
            keyMaterial,
            activatesWhen: DateTimeOffset.UnixEpoch.AddDays(2),
            retiresWhen: DateTimeOffset.UnixEpoch.AddDays(92),
            expiresWhen: DateTimeOffset.UnixEpoch.AddDays(99)
        );

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), secretKey.Metadata.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(92), secretKey.Metadata.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(99), secretKey.Metadata.ExpiresWhen);
    }

    #endregion

    #region DeserializeSecret ECC Tests

    [Fact]
    public void DeserializeSecret_WhenEcc_ReturnsEccKey()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        var pkcs8 = ecdh.ExportPkcs8PrivateKey();
        var protector = CreateProtector();

        var persisted = CreatePersistedSecret(protector, SecretTypes.Ecc, ecdh.KeySize, pkcs8);

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        var eccKey = Assert.IsAssignableFrom<EccSecretKey>(secretKey);
        Assert.Equal(384, eccKey.KeySizeBits);
        using var exported = eccKey.ExportECDiffieHellman();
        Assert.Equal(384, exported.KeySize);
    }

    #endregion

    #region DeserializeSecret Error Tests

    [Fact]
    public void DeserializeSecret_WhenUnsupportedType_ThrowsInvalidOperation()
    {
        var protector = CreateProtector();
        var persisted = CreatePersistedSecret(
            protector,
            "unsupported",
            128,
            [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(protector, persisted)
        );

        Assert.Contains("unsupported", exception.Message);
    }

    [Fact]
    public void DeserializeSecret_WhenTheSecretHoldsAVerifier_Throws()
    {
        var protector = CreateProtector();
        var persisted = CreateVerifierSecret(protector, Sha256Text("secret"));

        Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(protector, persisted)
        );
    }

    #endregion

    #region DeserializeSecrets Tests

    [Fact]
    public void DeserializeSecrets_WhenEmpty_ReturnsEmpty()
    {
        var protector = CreateProtector();

        var secretKeys = _serializer.DeserializeSecrets(protector, []);

        Assert.Empty(secretKeys);
    }

    [Fact]
    public void DeserializeSecrets_WhenMultiple_ReturnsAllSortedByExpiresDescending()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var protector = CreateProtector();

        var earlier = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            keyMaterial.Length * 8,
            CreateEnvelope(protector, "earlier", keyMaterial),
            secretId: "earlier",
            expiresWhen: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var later = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            keyMaterial.Length * 8,
            CreateEnvelope(protector, "later", keyMaterial),
            secretId: "later",
            expiresWhen: new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );

        var secretKeys = _serializer.DeserializeSecrets(protector, [earlier, later]);

        Assert.Equal(2, secretKeys.Count);
        Assert.Equal(["later", "earlier"], secretKeys.Select(x => x.KeyId));
    }

    [Fact]
    public void DeserializeSecrets_WhenASecretHoldsAVerifier_Throws()
    {
        // The resolver reads a client's key material with this overload, so a verifier never becomes a key.
        var protector = CreateProtector();
        var key = CreatePersistedSecret(
            protector,
            SecretTypes.Symmetric,
            256,
            RandomNumberGenerator.GetBytes(32),
            "key-1"
        );
        var verifier = CreateVerifierSecret(protector, Sha256Text("secret"), "verifier-1");

        Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecrets(protector, [key, verifier])
        );
    }

    #endregion

    #region DeserializeSecret Envelope Tests

    [Fact]
    public void DeserializeSecret_EnvelopeSymmetric_RoundTrips()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            keyMaterial.Length * 8,
            CreateEnvelope(protector, "secret-1", keyMaterial)
        );

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        var symmetric = Assert.IsAssignableFrom<SymmetricSecretKey>(secretKey);
        Assert.Equal("secret-1", symmetric.KeyId);
        Assert.Equal(keyMaterial.Length * 8, symmetric.KeySizeBits);

        var writer = new ArrayBufferWriter<byte>();
        symmetric.ExportPrivateKey(ref writer);
        Assert.Equal(keyMaterial, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void DeserializeSecret_EnvelopeRsaPkcs8_RoundTrips()
    {
        using var rsa = RSA.Create(2048);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        var expectedModulus = rsa.ExportParameters(false).Modulus;
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Rsa,
            SecretEncodings.Pkcs8,
            rsa.KeySize,
            CreateEnvelope(protector, "secret-1", pkcs8)
        );

        var secretKey = _serializer.DeserializeSecret(protector, persisted);

        var rsaKey = Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        using var exported = rsaKey.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    [Fact]
    public void DeserializeSecret_EnvelopeForOtherSecretId_Throws()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            keyMaterial.Length * 8,
            CreateEnvelope(protector, "secret-1", keyMaterial),
            secretId: "secret-2"
        );

        Assert.Throws<CryptographicException>(() =>
            _serializer.DeserializeSecret(protector, persisted)
        );
    }

    [Fact]
    public void DeserializeSecret_PrivatePlainBase64Url_Throws()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            keyMaterial.Length * 8,
            Base64Url.EncodeToString(keyMaterial)
        );

        Assert.Throws<CryptographicException>(() =>
            _serializer.DeserializeSecret(protector, persisted)
        );
    }

    #endregion

    #region DeserializePublicSecret Tests

    [Fact]
    public void DeserializePublicSecret_PlainSpki_Reads()
    {
        using var rsa = RSA.Create(2048);
        var spki = rsa.ExportSubjectPublicKeyInfo();
        var expectedModulus = rsa.ExportParameters(false).Modulus;
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Rsa,
            SecretEncodings.Spki,
            rsa.KeySize,
            Base64Url.EncodeToString(spki)
        );

        var secretKey = _serializer.DeserializePublicSecret(persisted);

        var rsaKey = Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        using var exported = rsaKey.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    [Fact]
    public void DeserializePublicSecret_PrivateEncoding_Throws()
    {
        using var rsa = RSA.Create(2048);
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Rsa,
            SecretEncodings.Pkcs8,
            rsa.KeySize,
            CreateEnvelope(protector, "secret-1", rsa.ExportPkcs8PrivateKey())
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializePublicSecret(persisted)
        );

        Assert.Contains(SecretEncodings.Pkcs8, exception.Message);
    }

    [Fact]
    public void DeserializePublicSecret_PrivateKeyWithStoredPublicKey_ReadsThePublicHalf()
    {
        using var rsa = RSA.Create(2048);
        var expectedModulus = rsa.ExportParameters(false).Modulus;
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Rsa,
            SecretEncodings.Pkcs8,
            rsa.KeySize,
            CreateEnvelope(protector, "secret-1", rsa.ExportPkcs8PrivateKey()),
            publicKey: Base64Url.EncodeToString(rsa.ExportSubjectPublicKeyInfo())
        );

        var secretKey = _serializer.DeserializePublicSecret(persisted);

        var rsaKey = Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        using var exported = rsaKey.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    [Fact]
    public void DeserializePublicSecret_Symmetric_Throws()
    {
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            256,
            CreateEnvelope(protector, "secret-1", RandomNumberGenerator.GetBytes(32))
        );

        Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializePublicSecret(persisted)
        );
    }

    #endregion

    #region DeserializeSecret Remote Tests

    [Fact]
    public void DeserializeSecret_WhenRemoteRsa_ReturnsARemoteKeyWithItsRowAndSize()
    {
        using var rsa = RSA.Create(2048);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256"
        );

        var secretKey = _serializer.DeserializeSecret(CreateProtector(), persisted);

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(secretKey);
        Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        Assert.Equal(2048, secretKey.KeySizeBits);
        Assert.Equal("secret-1", secretKey.KeyId);
        Assert.Equal("aws-kms:key-1", remoteKey.Binding.Reference.ToString());
        Assert.Equal(RemoteKeyProviderNames.AwsKms, remoteKey.Binding.Provider.Name);
        Assert.Same(RsaRow, remoteKey.Binding.Algorithm);
    }

    [Fact]
    public void DeserializeSecret_WhenRemoteEcc_ReturnsARemoteKeyWithItsCurve()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var persisted = CreateRemoteSecret(
            SecretTypes.Ecc,
            ecdsa.KeySize,
            ecdsa.ExportSubjectPublicKeyInfo(),
            "ES256"
        );

        var secretKey = _serializer.DeserializeSecret(CreateProtector(), persisted);

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(secretKey);
        var eccKey = Assert.IsAssignableFrom<EccSecretKey>(secretKey);
        Assert.Equal(256, eccKey.KeySizeBits);
        Assert.Equal("P-256", eccKey.CurveName);
        Assert.Same(EccRow, remoteKey.Binding.Algorithm);
    }

    [Fact]
    public void DeserializeSecrets_WhenRemote_ReadsTheRemoteKeyBesideThePrivateKeys()
    {
        using var rsa = RSA.Create(2048);
        var protector = CreateProtector();
        var remote = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256"
        );
        var local = CreatePersistedSecret(
            protector,
            SecretTypes.Symmetric,
            256,
            RandomNumberGenerator.GetBytes(32),
            "local-1"
        );

        var secretKeys = _serializer.DeserializeSecrets(protector, [remote, local]);

        Assert.Equal(2, secretKeys.Count);
        Assert.Single(secretKeys.OfType<IRemoteSecretKey>());
    }

    [Fact]
    public void DeserializeSecret_WhenTheRemoteReferenceIsMalformed_ThrowsFormatException()
    {
        using var rsa = RSA.Create(2048);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256",
            "no-separator"
        );

        Assert.Throws<FormatException>(() =>
            _serializer.DeserializeSecret(CreateProtector(), persisted)
        );
    }

    [Fact]
    public void DeserializeSecret_WhenTheRemoteProviderIsNotRegistered_ThrowsInvalidOperation()
    {
        using var rsa = RSA.Create(2048);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256",
            "other-kms:key-1"
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(CreateProtector(), persisted)
        );

        Assert.Contains("secret-1", exception.Message);
        Assert.Contains("other-kms", exception.Message);
    }

    [Fact]
    public void DeserializeSecret_WhenTheRemoteSecretHasNoPublicKey_ThrowsInvalidOperation()
    {
        var persisted = CreateRemoteSecret(SecretTypes.Rsa, 2048, [], "RS256");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(CreateProtector(), persisted)
        );

        Assert.Contains("secret-1", exception.Message);
    }

    [Fact]
    public void DeserializeSecret_WhenTheRemoteSecretHasNoAlgorithm_ThrowsInvalidOperation()
    {
        using var rsa = RSA.Create(2048);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            algorithm: null
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(CreateProtector(), persisted)
        );

        Assert.Contains("secret-1", exception.Message);
    }

    [Fact]
    public void DeserializeSecret_WhenNoRowMatchesTheRemoteSecret_ThrowsInvalidOperation()
    {
        using var rsa = RSA.Create(2048);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "PS256"
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(CreateProtector(), persisted)
        );

        Assert.Contains("secret-1", exception.Message);
        Assert.Contains("PS256", exception.Message);
    }

    [Fact]
    public void DeserializeSecret_WhenTheStoredKeyIsOfAnotherSizeThanTheRow_ThrowsInvalidOperation()
    {
        using var rsa = RSA.Create(3072);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256"
        );

        Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(CreateProtector(), persisted)
        );
    }

    [Fact]
    public void DeserializePublicSecret_WhenRemote_ReadsTheStoredPublicKeyWithoutTheCatalog()
    {
        using var rsa = RSA.Create(2048);
        var expectedModulus = rsa.ExportParameters(false).Modulus;
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256",
            "other-kms:key-1"
        );
        var serializer = new DefaultSecretSerializer(
            _catalog,
            TestSecretVerifierSchemes.CreateCatalog(),
            new DefaultRemoteKeyProviderCatalog([], new DefaultRemoteKeyDescriptorCatalog([])),
            new DefaultRemoteKeyDescriptorCatalog([])
        );

        var secretKey = serializer.DeserializePublicSecret(persisted);

        Assert.IsNotAssignableFrom<IRemoteSecretKey>(secretKey);
        var rsaKey = Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        using var exported = rsaKey.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    [Fact]
    public void DeserializeRemoteSecret_WhenRemote_ReturnsTheRemoteKeyWithoutAProtector()
    {
        using var rsa = RSA.Create(2048);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256"
        );

        var secretKey = _serializer.DeserializeRemoteSecret(persisted);

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(secretKey);
        Assert.Equal(2048, secretKey.KeySizeBits);
        Assert.Equal("aws-kms:key-1", remoteKey.Binding.Reference.ToString());
        Assert.Same(RsaRow, remoteKey.Binding.Algorithm);
    }

    [Fact]
    public void DeserializeRemoteSecret_WhenTheSecretIsNotRemote_ThrowsInvalidOperation()
    {
        var persisted = CreatePersistedSecret(
            CreateProtector(),
            SecretTypes.Symmetric,
            256,
            RandomNumberGenerator.GetBytes(32)
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeRemoteSecret(persisted)
        );

        Assert.Contains("secret-1", exception.Message);
    }

    [Fact]
    public void DeserializeRemoteSecret_WhenTheProviderIsNotRegistered_ThrowsInvalidOperation()
    {
        using var rsa = RSA.Create(2048);
        var persisted = CreateRemoteSecret(
            SecretTypes.Rsa,
            rsa.KeySize,
            rsa.ExportSubjectPublicKeyInfo(),
            "RS256",
            "other-kms:key-1"
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeRemoteSecret(persisted)
        );

        Assert.Contains("other-kms", exception.Message);
    }

    #endregion

    #region DeserializeVerifiers Tests

    // A scheme a package registers: its parameters are the secret itself, which only a test would store.
    private sealed class PlainTextSecretVerifierScheme : ISecretVerifierScheme
    {
        public const string SchemeName = "test-plain";

        public string Name => SchemeName;

        public void WriteParameters(ReadOnlySpan<byte> secret, IBufferWriter<byte> destination) =>
            destination.Write(secret);

        public bool TryReadParameters(
            KeyMetadata metadata,
            ReadOnlySpan<byte> parameters,
            [NotNullWhen(true)] out SecretVerifier? verifier
        )
        {
            verifier = new PlainTextSecretVerifier(metadata, parameters.ToArray());
            return true;
        }
    }

    // The verifier PlainTextSecretVerifierScheme reads, which compares the presented secret with the stored one.
    private sealed class PlainTextSecretVerifier(KeyMetadata metadata, byte[] secret)
        : SecretVerifier
    {
        public override KeyMetadata Metadata { get; } = metadata;

        public override string Scheme => PlainTextSecretVerifierScheme.SchemeName;

        public override bool HasWorkFactor => false;

        public override ValueTask<SecretVerificationResult> VerifyAsync(
            ReadOnlyMemory<byte> presented,
            CancellationToken cancellationToken
        ) =>
            ValueTask.FromResult(
                presented.Span.SequenceEqual(secret)
                    ? SecretVerificationResult.Success
                    : SecretVerificationResult.Failed
            );
    }

    [Fact]
    public async Task DeserializeVerifiers_ReadsEachVerifierWithItsMetadata()
    {
        var protector = CreateProtector();
        var first = CreateVerifierSecret(protector, Sha256Text("first"), "first");
        var second = CreateVerifierSecret(protector, Sha256Text("second"), "second", "kid-2");

        var verifiers = _serializer.DeserializeVerifiers(protector, [first, second]);

        Assert.Equal(2, verifiers.Count);
        var firstVerifier = verifiers.ElementAt(0);
        Assert.Equal("first", firstVerifier.KeyId);
        Assert.Equal(SecretVerifierSchemes.Sha256, firstVerifier.Scheme);
        Assert.Equal(first.ExpiresWhen, firstVerifier.Metadata.ExpiresWhen);
        Assert.Equal(
            SecretVerificationResult.Success,
            await firstVerifier.VerifyAsync("first"u8.ToArray(), CancellationToken.None)
        );
        Assert.Equal(
            SecretVerificationResult.Failed,
            await firstVerifier.VerifyAsync("second"u8.ToArray(), CancellationToken.None)
        );
        var secondVerifier = verifiers.ElementAt(1);
        Assert.Equal("kid-2", secondVerifier.KeyId);
        Assert.Equal(
            SecretVerificationResult.Success,
            await secondVerifier.VerifyAsync("second"u8.ToArray(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task DeserializeVerifiers_WhenAPackageRegistersAScheme_ReadsItsVerifiers()
    {
        var serializer = new DefaultSecretSerializer(
            _catalog,
            new DefaultSecretVerifierSchemeCatalog([new PlainTextSecretVerifierScheme()]),
            _remoteCatalog,
            _remoteDescriptorCatalog
        );
        var protector = CreateProtector();
        var persisted = CreateVerifierSecret(protector, "$test-plain$secret");

        var verifier = Assert.Single(serializer.DeserializeVerifiers(protector, [persisted]));

        Assert.Equal("test-plain", verifier.Scheme);
        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync("secret"u8.ToArray(), CancellationToken.None)
        );
    }

    [Fact]
    public void DeserializeVerifiers_WhenEmpty_ReturnsEmpty()
    {
        var protector = CreateProtector();

        Assert.Empty(_serializer.DeserializeVerifiers(protector, []));
    }

    [Fact]
    public void DeserializeVerifiers_WhenTheSecretHoldsKeyMaterial_Throws()
    {
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            256,
            CreateEnvelope(protector, "secret-1", RandomNumberGenerator.GetBytes(32))
        );

        Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeVerifiers(protector, [persisted])
        );
    }

    [Fact]
    public void DeserializeVerifiers_WhenTheVerifierIsOfAnUnknownScheme_ThrowsFormatException()
    {
        var protector = CreateProtector();
        var persisted = CreateVerifierSecret(protector, "$ncode-argon9$AAAA");

        Assert.Throws<FormatException>(() =>
            _serializer.DeserializeVerifiers(protector, [persisted])
        );
    }

    [Fact]
    public void DeserializeVerifiers_WhenTheVerifierIsNotAnEnvelope_ThrowsCryptographicException()
    {
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.Verifier,
            256,
            Sha256Text("plain")
        );

        Assert.Throws<CryptographicException>(() =>
            _serializer.DeserializeVerifiers(protector, [persisted])
        );
    }

    [Fact]
    public void DeserializeVerifiers_WhenTheEnvelopeBelongsToAnotherSecret_ThrowsCryptographicException()
    {
        var protector = CreateProtector();
        var persisted = CreatePersistedSecretFromEncodedValue(
            SecretTypes.Symmetric,
            SecretEncodings.Verifier,
            256,
            CreateEnvelope(
                protector,
                "another-secret",
                SecureEncoding.ASCII.GetBytes(Sha256Text("moved"))
            )
        );

        Assert.Throws<CryptographicException>(() =>
            _serializer.DeserializeVerifiers(protector, [persisted])
        );
    }

    #endregion
}
