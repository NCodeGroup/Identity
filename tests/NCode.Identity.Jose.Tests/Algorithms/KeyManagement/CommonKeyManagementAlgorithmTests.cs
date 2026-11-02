// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class CommonKeyManagementAlgorithmTests : BaseTests
{
    private Mock<KeyManagementAlgorithm> MockAlgorithm { get; }
    private DummyCommonKeyManagementAlgorithm Algorithm { get; }

    public CommonKeyManagementAlgorithmTests()
    {
        MockAlgorithm = CreatePartialMock<KeyManagementAlgorithm>();
        Algorithm = new DummyCommonKeyManagementAlgorithm(MockAlgorithm.Object);
    }

    [Fact]
    public void Type_ReturnsKeyManagement()
    {
        Assert.Equal(AlgorithmType.KeyManagement, Algorithm.Type);
    }

    [Fact]
    public void NewKey_GeneratesRandomKey()
    {
        const int kekSizeBytes = 32;
        const int kekSizeBits = kekSizeBytes << 3;
        const int cekSizeBytes = 64;

        Span<byte> kek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        var secretKey = SecretKeyFactory.CreateSymmetric(default, kek);

        Span<byte> cek = new byte[cekSizeBytes];
        cek.Clear();

        var header = new Dictionary<string, object>();

        MockAlgorithm
            .Setup(x => x.GetLegalCekByteSizes(kekSizeBits))
            .Returns(new[] { new KeySizes(cekSizeBytes, cekSizeBytes, 0) })
            .Verifiable();

        Algorithm.NewKey(secretKey, header, cek);

        Assert.NotEqual(0, cek.ToArray().Sum(b => b));
    }

    [Fact]
    public void ValidateContentKeySize_WhenValidSize_Succeeds()
    {
        const int kekSizeBytes = 32;
        const int kekSizeBits = kekSizeBytes << 3;
        const int cekSizeBytes = 64;

        MockAlgorithm
            .Setup(x => x.GetLegalCekByteSizes(kekSizeBits))
            .Returns(new[] { new KeySizes(cekSizeBytes, cekSizeBytes, 0) })
            .Verifiable();

        Algorithm.ValidateContentKeySize(kekSizeBits, cekSizeBytes);
    }

    [Fact]
    public void ValidateContentKeySize_WhenInvalidSize_Throws()
    {
        const int kekSizeBytes = 32;
        const int kekSizeBits = kekSizeBytes << 3;
        const int cekSizeBytes = 64;

        MockAlgorithm
            .Setup(x => x.GetLegalCekByteSizes(kekSizeBits))
            .Returns(new[] { new KeySizes(cekSizeBytes + 1, cekSizeBytes + 1, 0) })
            .Verifiable();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateContentKeySize(kekSizeBits, cekSizeBytes)
        );

        Assert.Equal(
            "The content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
            exception.Message
        );
    }
}
