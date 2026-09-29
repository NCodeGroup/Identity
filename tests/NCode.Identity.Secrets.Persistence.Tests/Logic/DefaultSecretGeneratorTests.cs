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

        _generator = new DefaultSecretGenerator(protectorFactory);
        _serializer = new DefaultSecretSerializer(secretKeyFactory, protectorFactory);
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

        var persisted = _generator.GenerateSecret(request);

        Assert.Equal("gen-1", persisted.SecretId);
        Assert.Equal(SecretTypes.Symmetric, persisted.SecretType);
        Assert.Equal(256, persisted.KeySizeBits);
        Assert.NotEmpty(persisted.EncodedValue);

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

        var persisted = _generator.GenerateSecret(request);

        Assert.Equal(SecretTypes.Rsa, persisted.SecretType);
        Assert.Equal(2048, persisted.KeySizeBits);

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

        var persisted = _generator.GenerateSecret(request);

        Assert.Equal(SecretTypes.Ecc, persisted.SecretType);
        Assert.Equal(256, persisted.KeySizeBits);

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

        var persisted = _generator.GenerateSecret(request);

        Assert.Equal(request.Use, persisted.Use);
        Assert.Equal(request.Algorithm, persisted.Algorithm);
        Assert.Equal(request.CreatedWhen, persisted.CreatedWhen);
        Assert.Equal(request.ExpiresWhen, persisted.ExpiresWhen);
    }

    [Fact]
    public void GenerateSecret_WhenUnsupportedSecretType_Throws()
    {
        var request = CreateRequest(SecretTypes.Certificate, 2048);

        Assert.Throws<InvalidOperationException>(() => _generator.GenerateSecret(request));
    }

    #endregion
}
