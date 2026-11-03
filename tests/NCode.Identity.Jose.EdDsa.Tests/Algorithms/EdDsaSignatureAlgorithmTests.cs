// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.Algorithms;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests.Algorithms;

public class EdDsaSignatureAlgorithmTests
{
    #region Helpers

    private static EdDsaSignatureAlgorithm CreateFixedAlgorithm(string code, string curveName)
    {
        Assert.True(OkpCurves.TryGetByName(curveName, out EdwardsOkpCurve? curve));
        return new(code, curve);
    }

    private sealed class StubOkpSecretKey(string curve, int keySizeBits)
        : OkpSecretKey(
            new KeyMetadata { KeyId = "kid-test" },
            curve,
            keySizeBits,
            new byte[keySizeBits / 8],
            certificateRawData: null
        )
    {
        public override bool HasPrivateKey => true;

        public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
            throw new NotSupportedException();
    }

    #endregion

    #region KeyTypes Property Tests

    [Theory]
    [InlineData(AlgorithmCodes.DigitalSignature.Ed25519, OkpCurves.Ed25519Name)]
    [InlineData(AlgorithmCodes.DigitalSignature.Ed448, OkpCurves.Ed448Name)]
    public void KeyTypes_WhenFullySpecified_NamesTheCurve(string code, string curveName)
    {
        var algorithm = CreateFixedAlgorithm(code, curveName);

        Assert.Equal(curveName, Assert.Single(Assert.Single(algorithm.KeyTypes).Curves).Name);
    }

    [Fact]
    public void KeyTypes_WhenPolymorphic_NamesBothSigningCurves()
    {
        var algorithm = new EdDsaSignatureAlgorithm(
            AlgorithmCodes.DigitalSignature.EdDsa,
            curve: null
        );

        Assert.Equal(
            [(OkpCurves.Ed25519Name, 256), (OkpCurves.Ed448Name, 456)],
            Assert
                .Single(algorithm.KeyTypes)
                .Curves.Select(curve => (curve.Name, curve.KeySizeBits))
        );
    }

    #endregion

    #region IsKeyCompatible Tests

    [Theory]
    [InlineData(AlgorithmCodes.DigitalSignature.Ed25519, OkpCurves.Ed25519Name, 256)]
    [InlineData(AlgorithmCodes.DigitalSignature.Ed448, OkpCurves.Ed448Name, 456)]
    public void IsKeyCompatible_WhenSizeAndCurveMatch_ReturnsTrue(
        string code,
        string curveName,
        int keySizeBits
    )
    {
        var algorithm = CreateFixedAlgorithm(code, curveName);
        var secretKey = new StubOkpSecretKey(curveName, keySizeBits);

        Assert.True(algorithm.IsKeyCompatible(secretKey));
    }

    [Theory]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed25519,
        OkpCurves.Ed25519Name,
        OkpCurves.Ed448Name,
        256
    )]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed448,
        OkpCurves.Ed448Name,
        OkpCurves.Ed25519Name,
        456
    )]
    public void IsKeyCompatible_WhenSizeMatchesAndCurveDiffers_ReturnsFalse(
        string code,
        string algorithmCurveName,
        string keyCurveName,
        int keySizeBits
    )
    {
        var algorithm = CreateFixedAlgorithm(code, algorithmCurveName);
        var secretKey = new StubOkpSecretKey(keyCurveName, keySizeBits);

        Assert.False(algorithm.IsKeyCompatible(secretKey));
    }

    // A key-agreement curve never signs. X25519 shares Ed25519's size; the X448 stub declares Ed448's size, so only the
    // curve's use refuses it.
    [Theory]
    [InlineData(AlgorithmCodes.DigitalSignature.EdDsa, null, OkpCurves.X25519Name, 256)]
    [InlineData(AlgorithmCodes.DigitalSignature.EdDsa, null, OkpCurves.X448Name, 456)]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed25519,
        OkpCurves.Ed25519Name,
        OkpCurves.X25519Name,
        256
    )]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed448,
        OkpCurves.Ed448Name,
        OkpCurves.X448Name,
        456
    )]
    public void IsKeyCompatible_WhenKeyAgreementCurve_ReturnsFalse(
        string code,
        string? algorithmCurveName,
        string keyCurveName,
        int keySizeBits
    )
    {
        var algorithm = algorithmCurveName is null
            ? new EdDsaSignatureAlgorithm(code, curve: null)
            : CreateFixedAlgorithm(code, algorithmCurveName);
        var secretKey = new StubOkpSecretKey(keyCurveName, keySizeBits);

        Assert.True(algorithm.TryGetKeyType(secretKey.GetType(), out var keyType));
        Assert.True(keyType.IsLegalSize(keySizeBits));
        Assert.False(algorithm.IsKeyCompatible(secretKey));
    }

    #endregion

    #region TrySign Tests

    [Fact]
    public void TrySign_WhenKeyAgreementCurve_Throws()
    {
        var algorithm = new EdDsaSignatureAlgorithm(
            AlgorithmCodes.DigitalSignature.EdDsa,
            curve: null
        );
        var secretKey = new StubOkpSecretKey(OkpCurves.X25519Name, 256);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            algorithm.TrySign(secretKey, "x"u8, new byte[64], out _)
        );

        Assert.Equal(
            "The OKP curve 'X25519' is not supported by the 'EdDSA' algorithm.",
            exception.Message
        );
    }

    [Theory]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed25519,
        OkpCurves.Ed25519Name,
        OkpCurves.Ed448Name,
        256
    )]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed448,
        OkpCurves.Ed448Name,
        OkpCurves.Ed25519Name,
        456
    )]
    public void TrySign_WhenSizeMatchesAndCurveDiffers_Throws(
        string code,
        string algorithmCurveName,
        string keyCurveName,
        int keySizeBits
    )
    {
        var algorithm = CreateFixedAlgorithm(code, algorithmCurveName);
        var secretKey = new StubOkpSecretKey(keyCurveName, keySizeBits);
        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            algorithm.TrySign(secretKey, "payload"u8, signature, out _)
        );

        Assert.Equal(
            $"The OKP curve '{keyCurveName}' is not supported by the '{code}' algorithm.",
            exception.Message
        );
    }

    #endregion

    #region Verify Tests

    [Theory]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed25519,
        OkpCurves.Ed25519Name,
        OkpCurves.Ed448Name,
        256
    )]
    [InlineData(
        AlgorithmCodes.DigitalSignature.Ed448,
        OkpCurves.Ed448Name,
        OkpCurves.Ed25519Name,
        456
    )]
    public void Verify_WhenSizeMatchesAndCurveDiffers_Throws(
        string code,
        string algorithmCurveName,
        string keyCurveName,
        int keySizeBits
    )
    {
        var algorithm = CreateFixedAlgorithm(code, algorithmCurveName);
        var secretKey = new StubOkpSecretKey(keyCurveName, keySizeBits);
        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            algorithm.Verify(secretKey, "payload"u8, signature)
        );

        Assert.Equal(
            $"The OKP curve '{keyCurveName}' is not supported by the '{code}' algorithm.",
            exception.Message
        );
    }

    #endregion
}
