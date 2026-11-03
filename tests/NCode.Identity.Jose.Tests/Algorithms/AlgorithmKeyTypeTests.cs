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

    [Fact]
    public void Constructor_WhenNoCurveIsNamed_LeavesCurvesEmpty()
    {
        var keyType = new AlgorithmKeyType(typeof(RsaSecretKey), []);

        Assert.Empty(keyType.Curves);
    }

    #endregion

    #region IsLegalCurve Tests

    [Fact]
    public void IsLegalCurve_WhenCurvesAreEmpty_ReturnsTrue()
    {
        var keyType = new AlgorithmKeyType(typeof(EccSecretKey), []);

        Assert.True(keyType.IsLegalCurve("P-256", 256));
    }

    [Theory]
    [InlineData("P-256", 256, true)]
    [InlineData("P-384", 384, true)]
    [InlineData("P-384", 256, false)]
    [InlineData("secp256k1", 256, false)]
    [InlineData("p-256", 256, false)]
    public void IsLegalCurve_WhenCurvesAreNamed_ReturnsWhetherTheCurveAndSizeAreOneOfThem(
        string curve,
        int keySizeBits,
        bool expected
    )
    {
        var keyType = new AlgorithmKeyType(typeof(EccSecretKey), [])
        {
            Curves = [new("P-256", 256), new("P-384", 384)],
        };

        Assert.Equal(expected, keyType.IsLegalCurve(curve, keySizeBits));
    }

    #endregion

    #region TryGetCurve Tests

    [Theory]
    [InlineData(256, "X25519")]
    [InlineData(448, "X448")]
    public void TryGetCurve_WhenACurveHasTheSize_ReturnsTheFirst(int keySizeBits, string expected)
    {
        var keyType = new AlgorithmKeyType(typeof(EccSecretKey), [])
        {
            Curves = [new("X25519", 256), new("Other25519", 256), new("X448", 448)],
        };

        Assert.True(keyType.TryGetCurve(keySizeBits, out var curve));
        Assert.Equal(expected, curve.Name);
    }

    [Fact]
    public void TryGetCurve_WhenNoCurveHasTheSize_ReturnsFalse()
    {
        var keyType = new AlgorithmKeyType(typeof(EccSecretKey), [])
        {
            Curves = [new("P-256", 256)],
        };

        Assert.False(keyType.TryGetCurve(384, out var curve));
        Assert.Null(curve);
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
