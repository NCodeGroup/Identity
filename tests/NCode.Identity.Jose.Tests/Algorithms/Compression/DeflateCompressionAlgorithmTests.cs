// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Jose;
using NCode.Identity.Jose.Algorithms.Compression;
using Nerdbank.Streams;

namespace NCode.Identity.Jose.Tests.Algorithms.Compression;

public class DeflateCompressionAlgorithmTests : BaseTests
{
    private Mock<IBufferWriter<byte>> MockBufferWriter { get; }
    private static DeflateCompressionAlgorithm Algorithm => DeflateCompressionAlgorithm.Singleton;
    private DeflateCompression ControlAlgorithm { get; } = new(256000L);

    public DeflateCompressionAlgorithmTests()
    {
        MockBufferWriter = CreateStrictMock<IBufferWriter<byte>>();
    }

    [Fact]
    public void Code_ReturnsDefCode()
    {
        var result = Algorithm.Code;
        Assert.Equal("DEF", result);
    }

    [Fact]
    public void Compress_GivenEmpty_ThenValid()
    {
        Algorithm.Compress(Span<byte>.Empty, MockBufferWriter.Object);
    }

    [Fact]
    public void Compress_WhenGivenData_ReturnsCompressed()
    {
        Span<byte> uncompressedData = new byte[4096];
        RandomNumberGenerator.Fill(uncompressedData);

        var expected = ControlAlgorithm.Compress(uncompressedData.ToArray());

        using var compressedData = new Sequence<byte>(ArrayPool<byte>.Shared);

        Algorithm.Compress(uncompressedData, compressedData);

        Assert.Equal(expected, compressedData.AsReadOnlySequence.ToArray());
    }

    [Fact]
    public void Decompress_GivenEmpty_ThenValid()
    {
        Algorithm.Decompress(Span<byte>.Empty, MockBufferWriter.Object);
    }

    [Fact]
    public void Decompress_WhenGivenData_ReturnsDecompressed()
    {
        Span<byte> uncompressedData1 = new byte[4096];
        RandomNumberGenerator.Fill(uncompressedData1);
        var compressedData = ControlAlgorithm.Compress(uncompressedData1.ToArray());

        var expected = ControlAlgorithm.Decompress(compressedData.ToArray());

        using var uncompressedData2 = new Sequence<byte>(ArrayPool<byte>.Shared);

        Algorithm.Decompress(compressedData, uncompressedData2);

        Assert.Equal(expected, uncompressedData2.AsReadOnlySequence.ToArray());
    }
}
