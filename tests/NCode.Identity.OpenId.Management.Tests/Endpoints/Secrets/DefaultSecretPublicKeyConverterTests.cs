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

using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

public sealed class DefaultSecretPublicKeyConverterTests : IDisposable
{
    private const string SamplePem =
        "-----BEGIN PUBLIC KEY-----\nMIIBSAMPLE\n-----END PUBLIC KEY-----";

    private MockRepository MockRepository { get; }
    private Mock<ISecretSerializer> MockSecretSerializer { get; }
    private Mock<ISecretKeyTypeCatalog> MockCatalog { get; }

    public DefaultSecretPublicKeyConverterTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockSecretSerializer = MockRepository.Create<ISecretSerializer>();
        MockCatalog = MockRepository.Create<ISecretKeyTypeCatalog>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    private void SetupCatalog(string keyType, bool supportsPublicKeyExport)
    {
        var mockDescriptor = MockRepository.Create<ISecretKeyTypeDescriptor>();
        mockDescriptor
            .Setup(x => x.SupportsPublicKeyExport)
            .Returns(supportsPublicKeyExport)
            .Verifiable();
        ISecretKeyTypeDescriptor? descriptor = mockDescriptor.Object;
        MockCatalog.Setup(x => x.TryGet(keyType, out descriptor)).Returns(true).Verifiable();
    }

    private static PersistedSecret CreateSecret(string secretType) =>
        new()
        {
            SecretId = "secret-1",
            ConcurrencyToken = "ct",
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = secretType,
            KeySizeBits = 2048,
            EncodedValue = "encoded",
        };

    [Fact]
    public void Convert_WhenSymmetric_ReturnsNull()
    {
        var secret = CreateSecret(SecretTypes.Symmetric);
        var mockKey = MockRepository.Create<SymmetricSecretKey>();
        MockSecretSerializer
            .Setup(x => x.DeserializeSecret(secret))
            .Returns(mockKey.Object)
            .Verifiable();

        var converter = new DefaultSecretPublicKeyConverter(
            MockSecretSerializer.Object,
            MockCatalog.Object,
            []
        );

        var result = converter.Convert(secret);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_WhenRsa_ReturnsJwkAndPem()
    {
        var secret = CreateSecret(SecretTypes.Rsa);

        var mockKey = MockRepository.Create<RsaSecretKey>();
        mockKey.Setup(x => x.KeyType).Returns(SecretKeyTypes.Rsa).Verifiable();
        mockKey.Setup(x => x.ExportSubjectPublicKeyInfoPem()).Returns(SamplePem).Verifiable();
        SetupCatalog(SecretKeyTypes.Rsa, supportsPublicKeyExport: true);

        JsonWebKey? jsonWebKey = new RsaJsonWebKey
        {
            KeyId = "secret-1",
            Modulus = "bW9k",
            Exponent = "AQAB",
        };
        var mockJwkConverter = MockRepository.Create<IJsonWebKeyConverter>();
        mockJwkConverter
            .Setup(x => x.TryConvert(It.IsAny<SecretKey>(), out jsonWebKey))
            .Returns(true)
            .Verifiable();

        MockSecretSerializer
            .Setup(x => x.DeserializeSecret(secret))
            .Returns(mockKey.Object)
            .Verifiable();

        var converter = new DefaultSecretPublicKeyConverter(
            MockSecretSerializer.Object,
            MockCatalog.Object,
            [mockJwkConverter.Object]
        );

        var result = converter.Convert(secret);

        Assert.NotNull(result);
        Assert.Equal("secret-1", result.SecretId);
        Assert.Equal(SecretTypes.Rsa, result.SecretType);
        Assert.Equal(SamplePem, result.Pem);
        Assert.Equal("RSA", result.Jwk.GetProperty("kty").GetString());
    }

    [Fact]
    public void Convert_WhenNoConverterRegistered_Throws()
    {
        var secret = CreateSecret(SecretTypes.Rsa);
        var mockKey = MockRepository.Create<RsaSecretKey>();
        mockKey.Setup(x => x.KeyType).Returns(SecretKeyTypes.Rsa).Verifiable();
        SetupCatalog(SecretKeyTypes.Rsa, supportsPublicKeyExport: true);
        MockSecretSerializer
            .Setup(x => x.DeserializeSecret(secret))
            .Returns(mockKey.Object)
            .Verifiable();

        var converter = new DefaultSecretPublicKeyConverter(
            MockSecretSerializer.Object,
            MockCatalog.Object,
            []
        );

        Assert.Throws<InvalidOperationException>(() => converter.Convert(secret));
    }
}
