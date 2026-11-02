// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Jose.Tests.Algorithms.Compression;

public class CompressionAlgorithmTests : BaseTests
{
    private Mock<CompressionAlgorithm> MockCompressionAlgorithm { get; }
    private CompressionAlgorithm CompressionAlgorithm { get; }

    public CompressionAlgorithmTests()
    {
        MockCompressionAlgorithm = CreatePartialMock<CompressionAlgorithm>();
        CompressionAlgorithm = MockCompressionAlgorithm.Object;
    }

    [Fact]
    public void Type_ReturnsCompression()
    {
        var result = CompressionAlgorithm.Type;
        Assert.Equal(AlgorithmType.Compression, result);
    }
}
