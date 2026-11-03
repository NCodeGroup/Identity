// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Persistence.Verifiers;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretSerializerTests
{
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("tenant-one");

    private readonly DefaultSecretKeyTypeCatalog _catalog;
    private readonly DefaultSecretSerializer _serializer;

    public DefaultSecretSerializerTests()
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
        _serializer = new DefaultSecretSerializer(
            catalog,
            TestSecretVerifierSchemes.CreateCatalog()
        );
    }

    private static PersistedSecret CreatePersistedSecret(
        ISecretProtector protector,
        string secretType,
        int keySizeBits,
        ReadOnlySpan<byte> rawBytes,
        string secretId = "secret-1",
        DateTimeOffset? expiresWhen = null,
        string? keyId = null
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
            keyId
        );

    private static PersistedSecret CreatePersistedSecretFromEncodedValue(
        string secretType,
        string secretEncoding,
        int keySizeBits,
        string encodedValue,
        string secretId = "secret-1",
        DateTimeOffset? expiresWhen = null,
        string? keyId = null,
        string? publicKey = null
    ) =>
        new()
        {
            SecretId = secretId,
            KeyId = keyId,
            Use = "sig",
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = expiresWhen ?? DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = secretType,
            SecretEncoding = secretEncoding,
            KeySizeBits = keySizeBits,
            EncodedValue = encodedValue,
            PublicKey = publicKey,
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

    #region DeserializeVerifiers Tests

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
            new DefaultSecretVerifierSchemeCatalog(
                [new PlainTextSecretVerifierScheme()],
                [
                    TestSecretVerifierSchemes.CreateDataSource(
                        PlainTextSecretVerifierScheme.SchemeName
                    ),
                ]
            )
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
