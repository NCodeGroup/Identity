// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms.Compression;
using Nerdbank.Streams;

namespace NCode.Identity.Jose.Tests.Algorithms.Compression;

public class NoneCompressionAlgorithmTests : BaseTests
{
    private Mock<IBufferWriter<byte>> MockBufferWriter { get; }
    private static NoneCompressionAlgorithm Algorithm => NoneCompressionAlgorithm.Singleton;

    public NoneCompressionAlgorithmTests()
    {
        MockBufferWriter = CreateStrictMock<IBufferWriter<byte>>();
    }

    [Fact]
    public void Code_ReturnsEmptyString()
    {
        var result = Algorithm.Code;
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Compress_GivenEmpty_ThenValid()
    {
        Algorithm.Compress(Span<byte>.Empty, MockBufferWriter.Object);
    }

    [Fact]
    public void Compress_ReturnsUncompressedData()
    {
        Span<byte> uncompressedData = new byte[4096];
        RandomNumberGenerator.Fill(uncompressedData);

        using var compressedData = new Sequence<byte>(ArrayPool<byte>.Shared);

        Algorithm.Compress(uncompressedData, compressedData);

        Assert.Equal(uncompressedData.ToArray(), compressedData.AsReadOnlySequence.ToArray());
    }

    [Fact]
    public void Decompress_GivenEmpty_ThenValid()
    {
        Algorithm.Decompress(Span<byte>.Empty, MockBufferWriter.Object);
    }

    [Fact]
    public void Decompress_ReturnsUncompressedData()
    {
        Span<byte> compressedData = new byte[4096];
        RandomNumberGenerator.Fill(compressedData);

        using var uncompressedData = new Sequence<byte>(ArrayPool<byte>.Shared);

        Algorithm.Decompress(compressedData, uncompressedData);

        Assert.Equal(compressedData.ToArray(), uncompressedData.AsReadOnlySequence.ToArray());
    }
}
