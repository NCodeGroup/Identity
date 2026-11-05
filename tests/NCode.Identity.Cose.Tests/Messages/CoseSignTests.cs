// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseSignTests
{
    private static byte[] Hex(string hex) => CoseTestKeys.Hex(hex);

    private static readonly byte[] Content = Hex(CoseTestKeys.Content);

    private static CoseVerificationParameters Parameters(
        int[] algorithms,
        params SecretKey[] keys
    ) => new() { ValidAlgorithms = algorithms, Keys = keys };

    private static (byte[] Encoded, SecretKey[] Keys) EncodeTwoSignatures(CoseTestHost host)
    {
        var first = host.KeyFactory.CreateEcc(
            new KeyMetadata(),
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );
        var second = host.KeyFactory.CreateEcc(
            new KeyMetadata(),
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );
        var encoded = host.Serializer.EncodeSign(
            Content,
            [
                new CoseSigner(new CoseCredentials(first, CoseAlgorithms.Es256)),
                new CoseSigner(new CoseCredentials(second, CoseAlgorithms.Es256)),
            ],
            new CoseEncodingOptions()
        );
        return (encoded, [first, second]);
    }

    private static string FailureCode(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    #region Verify Tests

    [Fact]
    public async Task Verify_EcdsaExample_ReportsTheSignerHeaders()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateEcP256(
            host.KeyFactory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            keyId: "11"
        );

        var verified = host.Serializer.Verify(
            Hex(CoseVectors.V4Sign),
            Parameters([CoseAlgorithmIdentifiers.Es256], key)
        );

        Assert.Equal(CoseMessageType.Sign, verified.MessageType);
        Assert.Equal(Content, verified.Content.ToArray());
        Assert.NotNull(verified.Signature);
        Assert.True(
            verified
                .Signature.UnprotectedHeaders[CoseHeaderLabels.KeyId]
                .TryGetByteString(out var kid)
        );
        Assert.Equal("11"u8.ToArray(), kid.ToArray());
    }

    [Fact]
    public async Task Verify_RsaPssExample_Verifies()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateRsaPublic(
            host.KeyFactory,
            CoseTestKeys.RsaN,
            CoseTestKeys.RsaE,
            "meriadoc.brandybuck@rsa.example"
        );

        var verified = host.Serializer.Verify(
            Hex(CoseVectors.V6RsaPss),
            Parameters([CoseAlgorithmIdentifiers.Ps256], key)
        );

        Assert.Equal(Content, verified.Content.ToArray());
        Assert.Equal(CoseAlgorithmIdentifiers.Ps256, verified.Algorithm.Identifier);
    }

    [Fact]
    public async Task Verify_WrongKey_ThrowsInvalidSignature()
    {
        await using var host = new CoseTestHost();
        var other = host.KeyFactory.CreateEcc(
            new KeyMetadata { KeyId = "11" },
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );

        var exception = Assert.Throws<CoseException>(() =>
            host.Serializer.Verify(
                Hex(CoseVectors.V4Sign),
                Parameters([CoseAlgorithmIdentifiers.Es256], other)
            )
        );

        Assert.Equal(CoseFailureCodes.InvalidSignature, exception.FailureCode);
    }

    [Fact]
    public async Task Verify_NoPermittedSigner_ThrowsAlgorithmNotPermitted()
    {
        await using var host = new CoseTestHost();
        var ecKey = host.KeyFactory.CreateEcc(
            new KeyMetadata { KeyId = "ec" },
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );
        var encoded = host.Serializer.EncodeSign(
            Content,
            [new CoseSigner(new CoseCredentials(ecKey, CoseAlgorithms.Es256))],
            new CoseEncodingOptions()
        );

        var exception = Assert.Throws<CoseException>(() =>
            host.Serializer.Verify(encoded, Parameters([CoseAlgorithmIdentifiers.Ps256], ecKey))
        );

        Assert.Equal(CoseFailureCodes.AlgorithmNotPermitted, exception.FailureCode);
    }

    [Fact]
    public async Task Verify_MoreSignaturesThanTheLimit_ThrowsLimitExceeded()
    {
        await using var encoder = new CoseTestHost();
        var (encoded, keys) = EncodeTwoSignatures(encoder);
        await using var host = new CoseTestHost(options => options.MaxSignatures = 1);

        Assert.Equal(
            CoseFailureCodes.LimitExceeded,
            FailureCode(() =>
                host.Serializer.Verify(encoded, Parameters([CoseAlgorithmIdentifiers.Es256], keys))
            )
        );
        Assert.Equal(
            CoseFailureCodes.LimitExceeded,
            FailureCode(() => host.Serializer.Inspect(encoded, null))
        );
    }

    [Fact]
    public async Task Verify_SignaturesAtTheLimit_Verifies()
    {
        await using var encoder = new CoseTestHost();
        var (encoded, keys) = EncodeTwoSignatures(encoder);
        await using var host = new CoseTestHost(options => options.MaxSignatures = 2);

        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Es256], keys)
        );

        Assert.Equal(Content, verified.Content.ToArray());
    }

    #endregion

    #region EncodeSign Tests

    [Fact]
    public async Task EncodeSign_UnpermittedSignerAndEs256Signer_VerifiesThroughTheEs256Signer()
    {
        await using var host = new CoseTestHost();
        var rsa = host.KeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, RSA.Create(2048));
        var ecKey = host.KeyFactory.CreateEcc(
            new KeyMetadata { KeyId = "ec" },
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );

        var encoded = host.Serializer.EncodeSign(
            Content,
            [
                new CoseSigner(new CoseCredentials(rsa, CoseAlgorithms.Ps256)),
                new CoseSigner(new CoseCredentials(ecKey, CoseAlgorithms.Es256)),
            ],
            new CoseEncodingOptions()
        );

        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Es256], rsa, ecKey)
        );
        Assert.Equal(CoseAlgorithmIdentifiers.Es256, verified.Algorithm.Identifier);
        Assert.Same(ecKey, verified.Key);
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeSign_DetachedContent_RoundTrips()
    {
        await using var host = new CoseTestHost();
        var ecKey = host.KeyFactory.CreateEcc(
            new KeyMetadata { KeyId = "ec" },
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );
        var encoded = host.Serializer.EncodeSign(
            Content,
            [new CoseSigner(new CoseCredentials(ecKey, CoseAlgorithms.Es256))],
            new CoseEncodingOptions { DetachContent = true, ExternalAad = Hex("0102") }
        );

        var verified = host.Serializer.Verify(
            encoded,
            new CoseVerificationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
                Keys = [ecKey],
                DetachedContent = Content,
                ExternalAad = Hex("0102"),
            }
        );

        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeSign_NoSigners_Throws()
    {
        await using var host = new CoseTestHost();

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.EncodeSign(Content, [], new CoseEncodingOptions())
        );
    }

    [Fact]
    public async Task EncodeSign_MoreSignersThanTheLimit_Throws()
    {
        await using var host = new CoseTestHost(options => options.MaxSignatures = 1);
        var ecKey = host.KeyFactory.CreateEcc(
            new KeyMetadata(),
            ECDsa.Create(ECCurve.NamedCurves.nistP256)
        );
        var signer = new CoseSigner(new CoseCredentials(ecKey, CoseAlgorithms.Es256));

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.EncodeSign(Content, [signer, signer], new CoseEncodingOptions())
        );
    }

    #endregion
}
