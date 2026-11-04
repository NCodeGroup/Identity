// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseMac0Tests
{
    private static byte[] Hex(string hex) => CoseTestKeys.Hex(hex);

    private static readonly byte[] Content = Hex(CoseTestKeys.Content);

    private static CoseVerificationParameters Parameters(int algorithm, SecretKey key) =>
        new() { ValidAlgorithms = [algorithm], Keys = [key] };

    private static string FailureCode(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    #region EncodeMac0 Tests

    [Fact]
    public async Task EncodeMac0_HmacExample_ProducesTheVectorBytes()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);

        var encoded = host.Serializer.EncodeMac0(
            Content,
            new CoseCredentials(key, CoseAlgorithms.HmacSha256),
            new CoseEncodingOptions()
        );

        Assert.Equal(CoseVectors.V7Mac0, Convert.ToHexString(encoded));
        var verified = host.Serializer.Verify(
            encoded,
            Parameters(CoseAlgorithmIdentifiers.HmacSha256, key)
        );
        Assert.Equal(Content, verified.Content.ToArray());
        Assert.Equal(CoseMessageType.Mac0, verified.MessageType);
    }

    [Fact]
    public async Task EncodeMac0_CwtVector_ProducesTheVectorBytes()
    {
        await using var host = new CoseTestHost(
            extraAlgorithms: [CoseAlgorithms.HmacSha256Truncated64]
        );
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacCwtKey);

        var encoded = host.Serializer.EncodeMac0(
            Hex(CoseVectors.ClaimsA1),
            new CoseCredentials(key, CoseAlgorithms.HmacSha256Truncated64),
            new CoseEncodingOptions()
        );

        Assert.Equal(CoseVectors.V15MacedCwt, Convert.ToHexString(encoded));
        var verified = host.Serializer.Verify(
            encoded,
            Parameters(CoseAlgorithmIdentifiers.HmacSha256Truncated64, key)
        );
        Assert.Equal(Hex(CoseVectors.ClaimsA1), verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeMac0_KeyShorterThanTheHash_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.EncodeMac0(
                Content,
                new CoseCredentials(key, CoseAlgorithms.HmacSha256),
                new CoseEncodingOptions()
            )
        );
    }

    [Fact]
    public async Task EncodeMac0_SignatureAlgorithm_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.EncodeMac0(
                Content,
                new CoseCredentials(key, CoseAlgorithms.Es256),
                new CoseEncodingOptions()
            )
        );
    }

    #endregion

    #region Verify Tests

    [Fact]
    public async Task Verify_FloatClaimsVector_ReturnsTheClaims()
    {
        await using var host = new CoseTestHost(
            extraAlgorithms: [CoseAlgorithms.HmacSha256Truncated64]
        );
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacCwtKey);

        var verified = host.Serializer.Verify(
            Hex(CoseVectors.V16MacedCwtFloat),
            Parameters(CoseAlgorithmIdentifiers.HmacSha256Truncated64, key)
        );

        Assert.Equal(Hex("A106FB41D584367C200000"), verified.Content.ToArray());
    }

    [Fact]
    public async Task Verify_UnknownTag_ThrowsUnsupportedMessageType()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);

        Assert.Equal(
            CoseFailureCodes.UnsupportedMessageType,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V21Mac0UnknownTag),
                    Parameters(CoseAlgorithmIdentifiers.HmacSha256, key)
                )
            )
        );
    }

    [Fact]
    public async Task Verify_TagOneByteShort_ThrowsInvalidSignature()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);
        var message = Hex(CoseVectors.V7Mac0);
        var shortened = new List<byte>(message);
        shortened.RemoveAt(shortened.Count - 1);
        shortened[^32] = 0x1F;

        Assert.Equal(
            CoseFailureCodes.InvalidSignature,
            FailureCode(() =>
                host.Serializer.Verify(
                    shortened.ToArray(),
                    Parameters(CoseAlgorithmIdentifiers.HmacSha256, key)
                )
            )
        );
    }

    [Fact]
    public async Task Verify_TruncatedHmacWithoutRegistration_ThrowsUnsupportedAlgorithm()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacCwtKey);

        Assert.Equal(
            CoseFailureCodes.UnsupportedAlgorithm,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V16MacedCwtFloat),
                    Parameters(CoseAlgorithmIdentifiers.HmacSha256Truncated64, key)
                )
            )
        );
    }

    [Fact]
    public async Task Verify_WrongKey_ThrowsInvalidSignature()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacCwtKey);

        Assert.Equal(
            CoseFailureCodes.InvalidSignature,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V7Mac0),
                    Parameters(CoseAlgorithmIdentifiers.HmacSha256, key)
                )
            )
        );
    }

    #endregion
}
