// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.KeyMaterial;

public class SecretKeyExtensionsTests
{
    private static KeySizes[] LegalSizes { get; } = [new(minSize: 256, maxSize: 256, skipSize: 0)];

    #region Validate Tests

    [Fact]
    public void Validate_WhenTypeAndSizeValid_ReturnsTypedKey()
    {
        var key = new TestSecretKey(new KeyMetadata(), keySizeBits: 256);

        var result = key.Validate<TestSecretKey>(LegalSizes);

        Assert.Same(key, result);
    }

    [Fact]
    public void Validate_WhenWrongType_ThrowsArgumentException()
    {
        SecretKey key = new TestSecretKey(new KeyMetadata(), keySizeBits: 256);

        var exception = Assert.Throws<ArgumentException>(() =>
            key.Validate<OtherSecretKey>(LegalSizes)
        );

        Assert.Equal("secretKey", exception.ParamName);
    }

    [Fact]
    public void Validate_WhenIllegalSize_ThrowsArgumentException()
    {
        var key = new TestSecretKey(new KeyMetadata(), keySizeBits: 128);

        var exception = Assert.Throws<ArgumentException>(() =>
            key.Validate<TestSecretKey>(LegalSizes)
        );

        Assert.Equal("secretKey", exception.ParamName);
    }

    #endregion

    private sealed class OtherSecretKey : SecretKey
    {
        public override string KeyType => "other";
        public override KeyMetadata Metadata => new();
        public override int KeySizeBits => 256;
    }
}
