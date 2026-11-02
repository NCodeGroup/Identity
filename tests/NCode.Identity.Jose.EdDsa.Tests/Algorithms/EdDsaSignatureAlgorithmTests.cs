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
        Assert.True(OkpCurves.TryGetByName(curveName, out var curve));
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

    #endregion

    #region TrySign Tests

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
