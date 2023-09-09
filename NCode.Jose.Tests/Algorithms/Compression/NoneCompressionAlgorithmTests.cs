#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Buffers;
using System.Security.Cryptography;
using NCode.Jose.Algorithms.Compression;
using Nerdbank.Streams;

namespace NCode.Jose.Tests.Algorithms.Compression;

public class NoneCompressionAlgorithmTests : BaseTests
{
    private Mock<IBufferWriter<byte>> MockBufferWriter { get; }
    private NoneCompressionAlgorithm Algorithm { get; } = new();

    public NoneCompressionAlgorithmTests()
    {
        MockBufferWriter = CreateStrictMock<IBufferWriter<byte>>();
    }

    [Fact]
    public void Code_Valid()
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
    public void Compress_Valid()
    {
        Span<byte> uncompressedData = new byte[4096];
        RandomNumberGenerator.Fill(uncompressedData);

        using var compressedData = new Sequence<byte>();

        Algorithm.Compress(uncompressedData, compressedData);

        Assert.Equal(uncompressedData.ToArray(), compressedData.AsReadOnlySequence.ToArray());
    }

    [Fact]
    public void Decompress_GivenEmpty_ThenValid()
    {
        Algorithm.Decompress(Span<byte>.Empty, MockBufferWriter.Object);
    }

    [Fact]
    public void Decompress_Valid()
    {
        Span<byte> compressedData = new byte[4096];
        RandomNumberGenerator.Fill(compressedData);

        using var uncompressedData = new Sequence<byte>();

        Algorithm.Decompress(compressedData, uncompressedData);

        Assert.Equal(compressedData.ToArray(), uncompressedData.AsReadOnlySequence.ToArray());
    }
}
