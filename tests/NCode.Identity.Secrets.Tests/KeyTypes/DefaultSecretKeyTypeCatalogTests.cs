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

using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;

namespace NCode.Identity.Secrets.KeyTypes;

public class DefaultSecretKeyTypeCatalogTests : BaseTests
{
    private static readonly ISecretKeyFactory Factory = Mock.Of<ISecretKeyFactory>();

    private Mock<ISecretKeyTypeDescriptor> CreateDescriptor(string keyType, string? jwkKeyType)
    {
        var mockDescriptor = CreateStrictMock<ISecretKeyTypeDescriptor>();
        mockDescriptor.Setup(x => x.KeyType).Returns(keyType).Verifiable();
        mockDescriptor
            .Setup(x => x.JwkKeyType)
            .Returns(jwkKeyType is null ? null : new JsonWebKeyTypeDescriptor(jwkKeyType, [], []))
            .Verifiable();
        return mockDescriptor;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTwoKeyTypesShareJwkKeyType_Throws()
    {
        var rsa = new RsaSecretKeyTypeDescriptor(Factory);
        var custom = CreateDescriptor("custom", "RSA");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSecretKeyTypeCatalog([rsa, custom.Object])
        );

        Assert.Contains("'RSA'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenKeyTypesHaveNoJwkKeyType_IndexesEveryKeyType()
    {
        var first = CreateDescriptor("first", null);
        var second = CreateDescriptor("second", null);

        var catalog = new DefaultSecretKeyTypeCatalog([first.Object, second.Object]);

        Assert.Equal(2, catalog.Descriptors.Count);
    }

    #endregion

    #region TryGetByJwkKeyType Tests

    [Fact]
    public void TryGetByJwkKeyType_WhenRegistered_ReturnsDescriptor()
    {
        var rsa = new RsaSecretKeyTypeDescriptor(Factory);
        var ecc = new EccSecretKeyTypeDescriptor(Factory, new DefaultEccCurveCatalog([]));
        var symmetric = new SymmetricSecretKeyTypeDescriptor(Factory);
        var catalog = new DefaultSecretKeyTypeCatalog([rsa, ecc, symmetric]);

        var found = catalog.TryGetByJwkKeyType("EC", out var descriptor);

        Assert.True(found);
        Assert.Same(ecc, descriptor);
    }

    [Fact]
    public void TryGetByJwkKeyType_WhenContributedKeyType_ReturnsDescriptor()
    {
        var rsa = new RsaSecretKeyTypeDescriptor(Factory);
        var contributed = CreateDescriptor("xkp", "XKP");
        var catalog = new DefaultSecretKeyTypeCatalog([rsa, contributed.Object]);

        var found = catalog.TryGetByJwkKeyType("XKP", out var descriptor);

        Assert.True(found);
        Assert.Same(contributed.Object, descriptor);
    }

    [Fact]
    public void TryGetByJwkKeyType_WhenUnknown_ReturnsFalse()
    {
        var catalog = new DefaultSecretKeyTypeCatalog([new RsaSecretKeyTypeDescriptor(Factory)]);

        var found = catalog.TryGetByJwkKeyType("OKP", out var descriptor);

        Assert.False(found);
        Assert.Null(descriptor);
    }

    [Fact]
    public void TryGetByJwkKeyType_WhenCaseDiffers_ReturnsFalse()
    {
        var catalog = new DefaultSecretKeyTypeCatalog([new RsaSecretKeyTypeDescriptor(Factory)]);

        var found = catalog.TryGetByJwkKeyType("rsa", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetByJwkKeyType_WhenKeyTypeIsOverridden_ReturnsLastRegistered()
    {
        var original = new RsaSecretKeyTypeDescriptor(Factory);
        var replacement = new RsaSecretKeyTypeDescriptor(Factory);
        var catalog = new DefaultSecretKeyTypeCatalog([original, replacement]);

        var found = catalog.TryGetByJwkKeyType("RSA", out var descriptor);

        Assert.True(found);
        Assert.Same(replacement, descriptor);
    }

    #endregion
}
