// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class AlgorithmKeyTypeTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTypeIsNotASecretKey_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AlgorithmKeyType(typeof(string), [])
        );

        Assert.Equal("keyType", exception.ParamName);
    }

    #endregion

    #region Accepts Tests

    [Fact]
    public void Accepts_WhenTypeDerivesFromTheKeyType_ReturnsTrue()
    {
        var keyType = new AlgorithmKeyType(typeof(AsymmetricSecretKey), []);

        Assert.True(keyType.Accepts(typeof(RsaSecretKey)));
        Assert.True(keyType.Accepts(typeof(AsymmetricSecretKey)));
    }

    [Fact]
    public void Accepts_WhenTypeDoesNotDeriveFromTheKeyType_ReturnsFalse()
    {
        var keyType = new AlgorithmKeyType(typeof(RsaSecretKey), []);

        Assert.False(keyType.Accepts(typeof(EccSecretKey)));
        Assert.False(keyType.Accepts(typeof(AsymmetricSecretKey)));
    }

    #endregion

    #region IsLegalSize Tests

    [Theory]
    [InlineData(256, true)]
    [InlineData(384, true)]
    [InlineData(521, true)]
    [InlineData(320, false)]
    [InlineData(512, false)]
    public void IsLegalSize_ChecksTheDeclaredSizes(int keySizeBits, bool expected)
    {
        var keyType = new AlgorithmKeyType(
            typeof(EccSecretKey),
            [new KeySizes(256, 384, 128), new KeySizes(521, 521, 0)]
        );

        Assert.Equal(expected, keyType.IsLegalSize(keySizeBits));
    }

    #endregion
}
