// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class DefaultAlgorithmCollectionTests : BaseTests
{
    [Fact]
    public void Algorithms_ReturnsExpected()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<Algorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1.Setup(x => x.Code).Returns("code1").Verifiable();
        mockAlgorithm2.Setup(x => x.Code).Returns("code2").Verifiable();
        mockAlgorithm3.Setup(x => x.Code).Returns("code3").Verifiable();

        mockAlgorithm1.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();
        mockAlgorithm2.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();
        mockAlgorithm3.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();

        var algorithms = new[]
        {
            mockAlgorithm1.Object,
            mockAlgorithm2.Object,
            mockAlgorithm3.Object,
        };

        Assert.Contains(mockAlgorithm1.Object, algorithms);
        Assert.Contains(mockAlgorithm2.Object, algorithms);
        Assert.Contains(mockAlgorithm3.Object, algorithms);
    }

    [Fact]
    public void TryGetSignatureAlgorithm_ReturnsExpected()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<SignatureAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1.Setup(x => x.Code).Returns("code1").Verifiable();
        mockAlgorithm2.Setup(x => x.Code).Returns("code2").Verifiable();
        mockAlgorithm3.Setup(x => x.Code).Returns("code3").Verifiable();

        mockAlgorithm1.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();
        mockAlgorithm2.Setup(x => x.Type).Returns(AlgorithmType.DigitalSignature).Verifiable();
        mockAlgorithm3.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();

        var algorithms = new DefaultAlgorithmCollection(
            new[] { mockAlgorithm1.Object, mockAlgorithm2.Object, mockAlgorithm3.Object }
        );
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetSignatureAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetSignatureAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetSignatureAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetKeyManagementAlgorithm_ReturnsExpected()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<KeyManagementAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1.Setup(x => x.Code).Returns("code1").Verifiable();
        mockAlgorithm2.Setup(x => x.Code).Returns("code2").Verifiable();
        mockAlgorithm3.Setup(x => x.Code).Returns("code3").Verifiable();

        mockAlgorithm1.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();
        mockAlgorithm2.Setup(x => x.Type).Returns(AlgorithmType.KeyManagement).Verifiable();
        mockAlgorithm3.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();

        var algorithms = new DefaultAlgorithmCollection(
            new[] { mockAlgorithm1.Object, mockAlgorithm2.Object, mockAlgorithm3.Object }
        );
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetKeyManagementAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetKeyManagementAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetKeyManagementAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetAuthenticatedEncryptionAlgorithm_ReturnsExpected()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<AuthenticatedEncryptionAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1.Setup(x => x.Code).Returns("code1").Verifiable();
        mockAlgorithm2.Setup(x => x.Code).Returns("code2").Verifiable();
        mockAlgorithm3.Setup(x => x.Code).Returns("code3").Verifiable();

        mockAlgorithm1.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();
        mockAlgorithm2
            .Setup(x => x.Type)
            .Returns(AlgorithmType.AuthenticatedEncryption)
            .Verifiable();
        mockAlgorithm3.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();

        var algorithms = new DefaultAlgorithmCollection(
            new[] { mockAlgorithm1.Object, mockAlgorithm2.Object, mockAlgorithm3.Object }
        );
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetAuthenticatedEncryptionAlgorithm(
            "code1",
            out var algorithm1
        );
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetAuthenticatedEncryptionAlgorithm(
            "code2",
            out var algorithm2
        );
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetAuthenticatedEncryptionAlgorithm(
            "code3",
            out var algorithm3
        );
        Assert.False(result3);
        Assert.Null(algorithm3);
    }

    [Fact]
    public void TryGetCompressionAlgorithm_ReturnsExpected()
    {
        var mockAlgorithm1 = CreateStrictMock<Algorithm>();
        var mockAlgorithm2 = CreateStrictMock<CompressionAlgorithm>();
        var mockAlgorithm3 = CreateStrictMock<Algorithm>();

        mockAlgorithm1.Setup(x => x.Code).Returns("code1").Verifiable();
        mockAlgorithm2.Setup(x => x.Code).Returns("code2").Verifiable();
        mockAlgorithm3.Setup(x => x.Code).Returns("code3").Verifiable();

        mockAlgorithm1.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();
        mockAlgorithm2.Setup(x => x.Type).Returns(AlgorithmType.Compression).Verifiable();
        mockAlgorithm3.Setup(x => x.Type).Returns(AlgorithmType.Unspecified).Verifiable();

        var algorithms = new DefaultAlgorithmCollection(
            new[] { mockAlgorithm1.Object, mockAlgorithm2.Object, mockAlgorithm3.Object }
        );
        Assert.Equal(3, algorithms.Count);

        var result1 = algorithms.TryGetCompressionAlgorithm("code1", out var algorithm1);
        Assert.False(result1);
        Assert.Null(algorithm1);

        var result2 = algorithms.TryGetCompressionAlgorithm("code2", out var algorithm2);
        Assert.True(result2);
        Assert.NotNull(algorithm2);

        var result3 = algorithms.TryGetCompressionAlgorithm("code3", out var algorithm3);
        Assert.False(result3);
        Assert.Null(algorithm3);
    }
}
