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

using Microsoft.AspNetCore.DataProtection;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

public class DefaultSecretGeneratorTests
{
    private readonly DefaultSecretGenerator _generator;
    private readonly DefaultSecretSerializer _serializer;

    public DefaultSecretGeneratorTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        var serializerProtector = provider.CreateProtector("serializer");
        var factoryProtector = provider.CreateProtector("factory");

        var protectorFactory = new TestDataProtectorFactory<PersistedSecret>(serializerProtector);

        var secretKeyFactory = new DefaultSecretKeyFactory(
            new TestDataProtectorFactory<SecretKey>(factoryProtector)
        );

        var catalog = new DefaultSecretKeyTypeCatalog([
            new SymmetricSecretKeyTypeDescriptor(secretKeyFactory),
            new RsaSecretKeyTypeDescriptor(secretKeyFactory),
            new EccSecretKeyTypeDescriptor(secretKeyFactory, new DefaultEccCurveCatalog([])),
        ]);

        _generator = new DefaultSecretGenerator(protectorFactory, catalog);
        _serializer = new DefaultSecretSerializer(catalog, protectorFactory);
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

    #region GenerateSecret Symmetric Tests

    [Fact]
    public void GenerateSecret_WhenSymmetric_ProducesRoundTrippableSecret()
    {
        var request = CreateRequest(SecretTypes.Symmetric, 256);

        var generated = _generator.GenerateSecret(request);
        var persisted = generated.Secret;

        Assert.Equal("gen-1", persisted.SecretId);
        Assert.Equal(SecretTypes.Symmetric, persisted.SecretType);
        Assert.Equal(256, persisted.KeySizeBits);
        Assert.NotEmpty(persisted.EncodedValue);

        // ADR-0050: symmetric secrets reveal their one-time unprotected material, distinct from the protected value.
        Assert.NotNull(generated.SecretMaterial);
        Assert.NotEmpty(generated.SecretMaterial);
        Assert.NotEqual(persisted.EncodedValue, generated.SecretMaterial);

        var secretKey = _serializer.DeserializeSecret(persisted);

        Assert.IsAssignableFrom<SymmetricSecretKey>(secretKey);
        Assert.Equal(256, secretKey.KeySizeBits);
    }

    [Fact]
    public void GenerateSecret_WhenSymmetricSizeNotMultipleOfEight_Throws()
    {
        var request = CreateRequest(SecretTypes.Symmetric, 100);

        Assert.Throws<ArgumentException>(() => _generator.GenerateSecret(request));
    }

    #endregion

    #region GenerateSecret Rsa Tests

    [Fact]
    public void GenerateSecret_WhenRsa_ProducesRoundTrippableSecret()
    {
        var request = CreateRequest(SecretTypes.Rsa, 2048);

        var generated = _generator.GenerateSecret(request);
        var persisted = generated.Secret;

        Assert.Equal(SecretTypes.Rsa, persisted.SecretType);
        Assert.Equal(2048, persisted.KeySizeBits);

        // ADR-0050: asymmetric private keys are never revealed.
        Assert.Null(generated.SecretMaterial);

        var secretKey = _serializer.DeserializeSecret(persisted);

        Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        Assert.Equal(2048, secretKey.KeySizeBits);
    }

    #endregion

    #region GenerateSecret Ecc Tests

    [Fact]
    public void GenerateSecret_WhenEcc_ProducesRoundTrippableSecret()
    {
        var request = CreateRequest(SecretTypes.Ecc, 256);

        var generated = _generator.GenerateSecret(request);
        var persisted = generated.Secret;

        Assert.Equal(SecretTypes.Ecc, persisted.SecretType);
        Assert.Equal(256, persisted.KeySizeBits);

        // ADR-0050: asymmetric private keys are never revealed.
        Assert.Null(generated.SecretMaterial);

        var secretKey = _serializer.DeserializeSecret(persisted);

        Assert.IsAssignableFrom<EccSecretKey>(secretKey);
        Assert.Equal(256, secretKey.KeySizeBits);
    }

    [Fact]
    public void GenerateSecret_WhenEccSizeUnsupported_Throws()
    {
        var request = CreateRequest(SecretTypes.Ecc, 300);

        Assert.Throws<ArgumentException>(() => _generator.GenerateSecret(request));
    }

    #endregion

    #region GenerateSecret Metadata Tests

    [Fact]
    public void GenerateSecret_CopiesMetadataFromRequest()
    {
        var request = CreateRequest(SecretTypes.Symmetric, 256);

        var persisted = _generator.GenerateSecret(request).Secret;

        Assert.Equal(request.Use, persisted.Use);
        Assert.Equal(request.Algorithm, persisted.Algorithm);
        Assert.Equal(request.CreatedWhen, persisted.CreatedWhen);
        Assert.Equal(request.ExpiresWhen, persisted.ExpiresWhen);
    }

    [Fact]
    public void GenerateSecret_WhenUnsupportedSecretType_Throws()
    {
        var request = CreateRequest("x509", 2048);

        Assert.Throws<InvalidOperationException>(() => _generator.GenerateSecret(request));
    }

    #endregion

    #region Public Key Export Tests

    [Fact]
    public void GenerateSecret_WhenRsa_ExportsSubjectPublicKeyInfoPem()
    {
        var generated = _generator.GenerateSecret(CreateRequest(SecretTypes.Rsa, 2048));

        var secretKey = _serializer.DeserializeSecret(generated.Secret);

        var asymmetric = Assert.IsAssignableFrom<AsymmetricSecretKey>(secretKey);
        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", asymmetric.ExportSubjectPublicKeyInfoPem());
    }

    [Fact]
    public void GenerateSecret_WhenEccWithCurveName_UsesThatCurveSize()
    {
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

        var persisted = _generator.GenerateSecret(request).Secret;

        Assert.Equal(384, persisted.KeySizeBits);
    }

    #endregion
}
