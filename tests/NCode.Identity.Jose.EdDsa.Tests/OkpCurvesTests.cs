// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests;

public class OkpCurvesTests
{
    #region TryGetByName Tests

    [Theory]
    [InlineData("Ed25519", "1.3.101.112", 256)]
    [InlineData("Ed448", "1.3.101.113", 456)]
    [InlineData("X25519", "1.3.101.110", 256)]
    [InlineData("X448", "1.3.101.111", 448)]
    public void TryGetByName_WhenCurveIsSupported_ReturnsIt(
        string name,
        string oid,
        int keySizeBits
    )
    {
        Assert.True(OkpCurves.TryGetByName(name, out var curve));

        Assert.Equal(name, curve.Name);
        Assert.Equal(oid, curve.Oid);
        Assert.Equal(keySizeBits, curve.KeySizeBits);
    }

    [Theory]
    [InlineData("ed25519")]
    [InlineData("P-256")]
    [InlineData(null)]
    public void TryGetByName_WhenCurveIsNotSupported_ReturnsFalse(string? name)
    {
        Assert.False(OkpCurves.TryGetByName(name, out var curve));
        Assert.Null(curve);
    }

    // A curve's kind is its use (RFC 8037 §3.1 and §3.2), so a key of one use never resolves as the other.
    [Theory]
    [InlineData("Ed25519", true, false)]
    [InlineData("Ed448", true, false)]
    [InlineData("X25519", false, true)]
    [InlineData("X448", false, true)]
    public void TryGetByName_WhenAKindIsAsked_ResolvesOnlyCurvesOfThatKind(
        string name,
        bool signs,
        bool agrees
    )
    {
        Assert.Equal(signs, OkpCurves.TryGetByName(name, out EdwardsOkpCurve? _));
        Assert.Equal(agrees, OkpCurves.TryGetByName(name, out MontgomeryOkpCurve? _));
    }

    #endregion

    #region TryGetByOid Tests

    [Theory]
    [InlineData("1.3.101.110", "X25519")]
    [InlineData("1.3.101.111", "X448")]
    [InlineData("1.3.101.112", "Ed25519")]
    [InlineData("1.3.101.113", "Ed448")]
    public void TryGetByOid_WhenCurveIsSupported_ReturnsIt(string oid, string expected)
    {
        Assert.True(OkpCurves.TryGetByOid(oid, out var curve));
        Assert.Equal(expected, curve.Name);
    }

    [Fact]
    public void TryGetByOid_WhenCurveIsNotSupported_ReturnsFalse()
    {
        Assert.False(OkpCurves.TryGetByOid("1.2.840.10045.3.1.7", out _));
    }

    #endregion

    #region TryGetSigningCurveByKeySizeBits Tests

    [Theory]
    [InlineData(256, "Ed25519")]
    [InlineData(456, "Ed448")]
    public void TryGetSigningCurveByKeySizeBits_WhenASigningCurveHasTheSize_ReturnsIt(
        int keySizeBits,
        string expected
    )
    {
        Assert.True(OkpCurves.TryGetSigningCurveByKeySizeBits(keySizeBits, out var curve));
        Assert.Equal(expected, curve.Name);
    }

    [Fact]
    public void TryGetSigningCurveByKeySizeBits_WhenOnlyAnAgreementCurveHasTheSize_ReturnsFalse()
    {
        Assert.False(OkpCurves.TryGetSigningCurveByKeySizeBits(448, out _));
    }

    #endregion

    #region GetTokenHashFunction Tests

    [Fact]
    public void GetTokenHashFunction_WhenEd25519_ReturnsSha512()
    {
        var hash = OkpCurves.Ed25519.GetTokenHashFunction(out var size);

        Assert.Equal(64, size);
        var destination = new byte[size];
        Assert.True(hash("abc"u8, destination, out var bytesWritten));
        Assert.Equal(size, bytesWritten);
        Assert.Equal(SHA512.HashData("abc"u8), destination);
    }

    [Fact]
    public void GetTokenHashFunction_WhenEd448_ReturnsShake256With114Bytes()
    {
        Assert.True(Shake256.IsSupported);

        var hash = OkpCurves.Ed448.GetTokenHashFunction(out var size);

        Assert.Equal(114, size);
        var destination = new byte[size];
        Assert.True(hash("abc"u8, destination, out var bytesWritten));
        Assert.Equal(size, bytesWritten);
        Assert.Equal(Shake256.HashData("abc"u8, 114), destination);
    }

    [Fact]
    public void GetTokenHashFunction_WhenEd448DestinationIsTooSmall_ReturnsFalse()
    {
        var hash = OkpCurves.Ed448.GetTokenHashFunction(out var size);
        var destination = new byte[size - 1];

        Assert.False(hash("abc"u8, destination, out var bytesWritten));
        Assert.Equal(0, bytesWritten);
        Assert.All(destination, value => Assert.Equal(0, value));
    }

    #endregion

    #region SupportedNames Property Tests

    [Fact]
    public void SupportedNames_ListsEveryCurveQuoted()
    {
        Assert.Equal("'Ed25519', 'Ed448', 'X25519', 'X448'", OkpCurves.SupportedNames);
    }

    #endregion
}
