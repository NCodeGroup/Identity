#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretGeneratorTests
{
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("tenant-one");

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

        _generator = new DefaultSecretGenerator(catalog);
        _serializer = new DefaultSecretSerializer(catalog);
    }

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

        // ADR-0050: symmetric secrets reveal their one-time unprotected material, distinct from the protected value.
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

    [Fact]
    public void GenerateSharedSecret_WhenNotSymmetric_Throws()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Rsa, 2048);

        Assert.Throws<InvalidOperationException>(() =>
            _generator.GenerateSharedSecret(protector, request)
        );
    }

    [Fact]
    public void GenerateSharedSecret_WhenSizeNotMultipleOfEight_Throws()
    {
        var protector = CreateProtector();
        var request = CreateRequest(SecretTypes.Symmetric, 100);

        Assert.Throws<ArgumentException>(() => _generator.GenerateSharedSecret(protector, request));
    }

    #endregion

    #region ImportSharedSecret Tests

    [Fact]
    public void ImportSharedSecret_ProducesTheSameStoredShapeAsGenerateSharedSecret()
    {
        var protector = CreateProtector();
        var generated = _generator.GenerateSharedSecret(
            protector,
            CreateRequest(SecretTypes.Symmetric, 256)
        );

        var imported = _generator.ImportSharedSecret(
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

        // ADR-0050: asymmetric private keys are never revealed.
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

        // ADR-0050: asymmetric private keys are never revealed.
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
    public void GenerateSharedSecret_StoresUtf8OfRevealedSecret()
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
        Assert.Equal(SecretEncodings.SymmetricRaw, persisted.SecretEncoding);
        Assert.NotNull(generated.SecretMaterial);
        Assert.NotEmpty(generated.SecretMaterial);

        var secretKey = Assert.IsAssignableFrom<SymmetricSecretKey>(
            _serializer.DeserializeSecret(protector, persisted)
        );

        var writer = new ArrayBufferWriter<byte>();
        secretKey.ExportPrivateKey(ref writer);

        // The core contract (CommonClientAuthenticationHandler.IsSecretEqual): client authentication compares the UTF-8
        // bytes of the presented secret against the stored key material, so the stored material must be exactly the
        // UTF-8 bytes of the revealed opaque string — otherwise a generated client_secret can never authenticate.
        var expected = SecureEncoding.UTF8.GetBytes(generated.SecretMaterial);
        Assert.Equal(expected, writer.WrittenSpan.ToArray());
        Assert.Equal(expected.Length * 8, persisted.KeySizeBits);
    }

    [Fact]
    public void ImportSharedSecret_StoresTheGivenUtf8BytesVerbatim()
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
        Assert.Equal(SecretEncodings.SymmetricRaw, persisted.SecretEncoding);
        Assert.Equal(secretValue.Length * 8, persisted.KeySizeBits);

        var secretKey = Assert.IsAssignableFrom<SymmetricSecretKey>(
            _serializer.DeserializeSecret(protector, persisted)
        );

        var writer = new ArrayBufferWriter<byte>();
        secretKey.ExportPrivateKey(ref writer);

        Assert.Equal(secretValue, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void ImportSharedSecret_WhenValueEmpty_Throws()
    {
        var protector = CreateProtector();
        var request = CreateImportRequest("import-2");

        Assert.Throws<ArgumentException>(() =>
            _generator.ImportSharedSecret(protector, request, ReadOnlySpan<byte>.Empty)
        );
    }

    #endregion
}
