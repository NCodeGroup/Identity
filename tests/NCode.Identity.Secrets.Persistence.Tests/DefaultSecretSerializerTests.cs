#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretSerializerTests
{
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("tenant-one");

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

        _serializer = new DefaultSecretSerializer(catalog);
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
        string? keyId = null
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

    #endregion
}
