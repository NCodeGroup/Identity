// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class AlgorithmKeyTypeTests : BaseTests
{
    private static AlgorithmKeyType CreateNistKeyType() =>
        AlgorithmKeyType.OnCurves(typeof(EccSecretKey), [new("P-256", 256), new("P-384", 384)]);

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTypeIsNotASecretKey_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AlgorithmKeyType(typeof(string), [])
        );

        Assert.Equal("keyClrType", exception.ParamName);
    }

    [Fact]
    public void Constructor_LeavesCurvesEmpty()
    {
        var keyType = new AlgorithmKeyType(typeof(RsaSecretKey), []);

        Assert.Empty(keyType.Curves);
    }

    #endregion

    #region OnCurves Tests

    [Fact]
    public void OnCurves_TakesTheSizesFromTheCurves()
    {
        var keyType = AlgorithmKeyType.OnCurves(
            typeof(EccSecretKey),
            [new("X25519", 256), new("Other25519", 256), new("X448", 448)]
        );

        Assert.Equal(typeof(EccSecretKey), keyType.KeyClrType);
        Assert.Equal(
            [("X25519", 256), ("Other25519", 256), ("X448", 448)],
            keyType.Curves.Select(curve => (curve.Name, curve.KeySizeBits))
        );
        Assert.Collection(
            keyType.KeyBitSizes,
            size => Assert.Equal((256, 256, 0), (size.MinSize, size.MaxSize, size.SkipSize)),
            size => Assert.Equal((448, 448, 0), (size.MinSize, size.MaxSize, size.SkipSize))
        );
    }

    [Fact]
    public void OnCurves_WhenNoCurveIsNamed_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AlgorithmKeyType.OnCurves(typeof(EccSecretKey), [])
        );

        Assert.Equal("curves", exception.ParamName);
    }

    [Fact]
    public void OnCurves_WhenTwoCurvesShareAName_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AlgorithmKeyType.OnCurves(typeof(EccSecretKey), [new("P-256", 256), new("P-256", 256)])
        );

        Assert.Equal("curves", exception.ParamName);
    }

    [Fact]
    public void OnCurves_WhenTypeIsNotASecretKey_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AlgorithmKeyType.OnCurves(typeof(string), [new("P-256", 256)])
        );

        Assert.Equal("keyClrType", exception.ParamName);
    }

    #endregion

    #region IsLegalCurve Tests

    [Fact]
    public void IsLegalCurve_WhenCurvesAreEmpty_ReturnsTrue()
    {
        var keyType = new AlgorithmKeyType(typeof(EccSecretKey), []);

        Assert.True(keyType.IsLegalCurve("P-256", 256));
        Assert.True(keyType.IsLegalCurve(null, 256));
    }

    [Theory]
    [InlineData("P-256", 256, true)]
    [InlineData("P-384", 384, true)]
    [InlineData("P-384", 256, false)]
    [InlineData("secp256k1", 256, false)]
    [InlineData("p-256", 256, false)]
    [InlineData(null, 256, false)]
    public void IsLegalCurve_WhenCurvesAreNamed_ReturnsWhetherTheCurveAndSizeAreOneOfThem(
        string? curve,
        int keySizeBits,
        bool expected
    )
    {
        var keyType = CreateNistKeyType();

        Assert.Equal(expected, keyType.IsLegalCurve(curve, keySizeBits));
    }

    #endregion

    #region IsKeyCompatible Tests

    [Fact]
    public void IsKeyCompatible_WhenKeyIsOnOneOfTheCurves_ReturnsTrue()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var key = SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "p384" }, ecdsa);

        Assert.True(CreateNistKeyType().IsKeyCompatible(key));
    }

    // brainpoolP256r1 has P-256's size, and the catalog does not name it, so only its curve tells it apart.
    [Fact]
    public void IsKeyCompatible_WhenKeyIsOnAnotherCurveOfTheSameSize_ReturnsFalse()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);
        var key = SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "brainpool" }, ecdsa);

        Assert.Equal(256, key.KeySizeBits);
        Assert.Null(key.CurveName);
        Assert.False(CreateNistKeyType().IsKeyCompatible(key));
    }

    [Fact]
    public void IsKeyCompatible_WhenKeyIsOfAnotherType_ReturnsFalse()
    {
        using var rsa = RSA.Create(2048);
        var key = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);

        Assert.False(CreateNistKeyType().IsKeyCompatible(key));
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(1024, false)]
    public void IsKeyCompatible_WhenTypeHasNoCurves_ChecksTheSize(int keySizeBits, bool expected)
    {
        using var rsa = RSA.Create(keySizeBits);
        var key = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);
        var keyType = new AlgorithmKeyType(
            typeof(RsaSecretKey),
            [new KeySizes(minSize: 2048, maxSize: 4096, skipSize: 1024)]
        );

        Assert.Equal(expected, keyType.IsKeyCompatible(key));
    }

    #endregion

    #region TryGetCurve Tests

    [Theory]
    [InlineData(256, "X25519")]
    [InlineData(448, "X448")]
    public void TryGetCurve_WhenACurveHasTheSize_ReturnsTheFirst(int keySizeBits, string expected)
    {
        var keyType = AlgorithmKeyType.OnCurves(
            typeof(EccSecretKey),
            [new("X25519", 256), new("Other25519", 256), new("X448", 448)]
        );

        Assert.True(keyType.TryGetCurve(keySizeBits, out var curve));
        Assert.Equal(expected, curve.Name);
    }

    [Fact]
    public void TryGetCurve_WhenNoCurveHasTheSize_ReturnsFalse()
    {
        var keyType = CreateNistKeyType();

        Assert.False(keyType.TryGetCurve(521, out var curve));
        Assert.Null(curve);
    }

    [Theory]
    [InlineData("P-384", true)]
    [InlineData("p-384", false)]
    [InlineData("secp256k1", false)]
    public void TryGetCurve_WhenGivenAName_ReturnsTheCurveOfThatName(string name, bool expected)
    {
        var keyType = CreateNistKeyType();

        Assert.Equal(expected, keyType.TryGetCurve(name, out var curve));
        Assert.Equal(expected ? name : null, curve?.Name);
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
