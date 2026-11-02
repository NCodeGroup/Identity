// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Extensions;

public class SecretKeyExtensionsTests : BaseTests
{
    [Fact]
    public void Validate_SymmetricKeyWithLegalSize_ReturnsSameKey()
    {
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        var secretKey = SecretKeyFactory.CreateSymmetric(default, key);

        var legalKeyBitSizes = new[] { new KeySizes(keySizeBits, keySizeBits, 0) };

        var result = secretKey.Validate<SymmetricSecretKey>(legalKeyBitSizes);
        Assert.Same(secretKey, result);
    }

    [Fact]
    public void Validate_InvalidType()
    {
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        var secretKey = SecretKeyFactory.CreateSymmetric(default, key);

        var legalKeyBitSizes = new[] { new KeySizes(keySizeBits, keySizeBits, 0) };

        var exception = Assert.Throws<ArgumentException>(() =>
            secretKey.Validate<AsymmetricSecretKey>(legalKeyBitSizes)
        );

        Assert.Equal(
            $"The secret key was expected to be a type of '{typeof(AsymmetricSecretKey).FullName}', but '{secretKey.GetType().FullName}' was given instead. (Parameter 'secretKey')",
            exception.Message
        );
    }

    [Fact]
    public void Validate_InvalidSize()
    {
        const int keySizeBytes = 32;
        const int keySizeBits = keySizeBytes * 8;

        Span<byte> key = new byte[keySizeBytes];
        var secretKey = SecretKeyFactory.CreateSymmetric(default, key);

        var legalKeyBitSizes = new[] { new KeySizes(keySizeBits + 8, keySizeBits + 8, 0) };

        var exception = Assert.Throws<ArgumentException>(() =>
            secretKey.Validate<SymmetricSecretKey>(legalKeyBitSizes)
        );

        Assert.Equal(
            "The secret key does not have a valid size for this cryptographic algorithm. (Parameter 'secretKey')",
            exception.Message
        );
    }
}
