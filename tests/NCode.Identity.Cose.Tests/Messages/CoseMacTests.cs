// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseMacTests
{
    private static byte[] Hex(string hex) => CoseTestKeys.Hex(hex);

    private static readonly byte[] Content = Hex(CoseTestKeys.Content);

    private static CoseVerificationParameters Parameters(
        int[] algorithms,
        int[] keyDistribution,
        params SecretKey[] keys
    ) =>
        new()
        {
            ValidAlgorithms = algorithms,
            ValidKeyDistributionAlgorithms = keyDistribution,
            Keys = keys,
        };

    private static string FailureCode(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    private static CoseRecipients Direct(CoseTestHost host, CoseAlgorithm contentAlgorithm) =>
        new(
            contentAlgorithm,
            [
                new CoseRecipient(
                    new CoseCredentials(
                        CoseTestKeys.CreateSymmetric(
                            host.KeyFactory,
                            CoseTestKeys.HmacKey,
                            "our-secret"
                        ),
                        CoseAlgorithms.Direct
                    )
                ),
            ]
        );

    #region Direct recipients

    [Fact]
    public async Task EncodeMac_DirectRecipient_ProducesTheVectorBytes()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "our-secret");

        var encoded = host.Serializer.EncodeMac(
            Content,
            Direct(host, CoseAlgorithms.HmacSha256),
            new CoseEncodingOptions()
        );

        Assert.Equal(CoseVectors.V8Mac, Convert.ToHexString(encoded));
        var verified = host.Serializer.Verify(
            encoded,
            Parameters(
                [CoseAlgorithmIdentifiers.HmacSha256],
                [CoseAlgorithmIdentifiers.Direct],
                key
            )
        );
        Assert.Equal(Content, verified.Content.ToArray());
        Assert.NotNull(verified.Recipient);
        Assert.Same(key, verified.Key);
    }

    [Fact]
    public async Task EncodeMac_TruncatedHmacRegistered_ProducesTheVectorBytes()
    {
        await using var host = new CoseTestHost(
            extraAlgorithms: [CoseAlgorithms.HmacSha256Truncated64]
        );
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "our-secret");

        var encoded = host.Serializer.EncodeMac(
            Content,
            Direct(host, CoseAlgorithms.HmacSha256Truncated64),
            new CoseEncodingOptions()
        );

        Assert.Equal(CoseVectors.V9MacTruncated, Convert.ToHexString(encoded));
        var verified = host.Serializer.Verify(
            encoded,
            Parameters(
                [CoseAlgorithmIdentifiers.HmacSha256Truncated64],
                [CoseAlgorithmIdentifiers.Direct],
                key
            )
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task Verify_TruncatedHmacWithoutRegistration_ThrowsUnsupportedAlgorithm()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "our-secret");

        Assert.Equal(
            CoseFailureCodes.UnsupportedAlgorithm,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V9MacTruncated),
                    Parameters(
                        [CoseAlgorithmIdentifiers.HmacSha256Truncated64],
                        [CoseAlgorithmIdentifiers.Direct],
                        key
                    )
                )
            )
        );
    }

    [Fact]
    public async Task Verify_ChangedTag_ThrowsInvalidSignature()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "our-secret");

        Assert.Equal(
            CoseFailureCodes.InvalidSignature,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V10MacChangedTag),
                    Parameters(
                        [CoseAlgorithmIdentifiers.HmacSha256],
                        [CoseAlgorithmIdentifiers.Direct],
                        key
                    )
                )
            )
        );
    }

    [Fact]
    public async Task Verify_RecipientAlgorithmNotPermitted_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "our-secret");

        Assert.Equal(
            CoseFailureCodes.AlgorithmNotPermitted,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V8Mac),
                    Parameters(
                        [CoseAlgorithmIdentifiers.HmacSha256],
                        [CoseAlgorithmIdentifiers.A128Kw],
                        key
                    )
                )
            )
        );
    }

    [Fact]
    public async Task Verify_KeyIdOfAnotherKey_ThrowsKeyNotFound()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "other");

        Assert.Equal(
            CoseFailureCodes.KeyNotFound,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V8Mac),
                    Parameters(
                        [CoseAlgorithmIdentifiers.HmacSha256],
                        [CoseAlgorithmIdentifiers.Direct],
                        key
                    )
                )
            )
        );
    }

    [Fact]
    public async Task Verify_DirectRecipientBesideAnother_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "our-secret");
        var recipient = "8340A20125044A6F75722D73656372657440";
        var message = Hex(
            "D8618543A10105A054"
                + CoseTestKeys.Content
                + "5820"
                + new string('0', 64)
                + "82"
                + recipient
                + recipient
        );

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(() =>
                host.Serializer.Verify(
                    message,
                    Parameters(
                        [CoseAlgorithmIdentifiers.HmacSha256],
                        [CoseAlgorithmIdentifiers.Direct],
                        key
                    )
                )
            )
        );
    }

    [Fact]
    public async Task EncodeMac_DirectRecipientBesideAnother_Throws()
    {
        await using var host = new CoseTestHost();
        var direct = new CoseRecipient(
            new CoseCredentials(
                CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "a"),
                CoseAlgorithms.Direct
            )
        );
        var wrap = new CoseRecipient(
            new CoseCredentials(
                CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "b"),
                CoseAlgorithms.A256Kw
            )
        );

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.EncodeMac(
                Content,
                new CoseRecipients(CoseAlgorithms.HmacSha256, [direct, wrap]),
                new CoseEncodingOptions()
            )
        );
    }

    #endregion

    #region Key wrap recipients

    [Fact]
    public async Task EncodeMac_TwoKeyWrapRecipients_VerifiesWithTheSecondKey()
    {
        var random = new QueueRandomSource();
        random.Enqueue("00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF");
        await using var host = new CoseTestHost(randomSource: random);
        var first = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "k1");
        var second = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacCwtKey, "k2");

        var encoded = host.Serializer.EncodeMac(
            Content,
            new CoseRecipients(
                CoseAlgorithms.HmacSha256,
                [
                    new CoseRecipient(new CoseCredentials(first, CoseAlgorithms.A256Kw)),
                    new CoseRecipient(new CoseCredentials(second, CoseAlgorithms.A256Kw)),
                ]
            ),
            new CoseEncodingOptions()
        );

        Assert.Equal(0, random.Remaining);
        Assert.Equal(2, host.Serializer.Inspect(encoded, null).Recipients.Count);
        var verified = host.Serializer.Verify(
            encoded,
            Parameters(
                [CoseAlgorithmIdentifiers.HmacSha256],
                [CoseAlgorithmIdentifiers.A256Kw],
                second
            )
        );
        Assert.Equal(Content, verified.Content.ToArray());
        Assert.Same(second, verified.Key);
    }

    [Fact]
    public async Task Verify_KeyWrapWithTheWrongKey_ThrowsInvalidSignature()
    {
        await using var host = new CoseTestHost();
        var kek = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "k1");
        var encoded = host.Serializer.EncodeMac(
            Content,
            new CoseRecipients(
                CoseAlgorithms.HmacSha256,
                [new CoseRecipient(new CoseCredentials(kek, CoseAlgorithms.A256Kw))]
            ),
            new CoseEncodingOptions()
        );
        var other = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacCwtKey, "k1");

        Assert.Equal(
            CoseFailureCodes.InvalidSignature,
            FailureCode(() =>
                host.Serializer.Verify(
                    encoded,
                    Parameters(
                        [CoseAlgorithmIdentifiers.HmacSha256],
                        [CoseAlgorithmIdentifiers.A256Kw],
                        other
                    )
                )
            )
        );
    }

    #endregion
}
