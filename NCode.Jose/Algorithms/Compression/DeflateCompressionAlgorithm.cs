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
using System.IO.Compression;
using System.Runtime.InteropServices;
using Nerdbank.Streams;

namespace NCode.Jose.Algorithms.Compression;

/// <summary>
/// Provides an implementation of <see cref="CompressionAlgorithm"/> that uses the <c>DEFLATE (RFC1951)</c> algorithm for compression.
/// </summary>
public class DeflateCompressionAlgorithm : CompressionAlgorithm
{
    /// <summary>
    /// Gets a singleton instance of <see cref="DeflateCompressionAlgorithm"/>.
    /// </summary>
    public static DeflateCompressionAlgorithm Singleton { get; } = new();

    /// <inheritdoc />
    public override string Code => AlgorithmCodes.Compression.Deflate;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeflateCompressionAlgorithm"/> class.
    /// </summary>
    private DeflateCompressionAlgorithm()
    {
        // nothing
    }

    /// <inheritdoc />
    public override void Compress(ReadOnlySpan<byte> uncompressedData, IBufferWriter<byte> compressedData)
    {
        if (uncompressedData.IsEmpty) return;

        using var compressedStream = compressedData.AsStream();
        using var deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal);

        deflateStream.Write(uncompressedData);
    }

    /// <inheritdoc />
    public override unsafe void Decompress(ReadOnlySpan<byte> compressedData, IBufferWriter<byte> uncompressedData)
    {
        if (compressedData.IsEmpty) return;

        // perf: use GetReference vs GetPinnableReference
        // https://github.com/dotnet/runtime/issues/27308
        // https://github.com/dotnet/runtime/issues/29003

        fixed (byte* pCompressedData = &MemoryMarshal.GetReference(compressedData))
        {
            using var compressedStream = new UnmanagedMemoryStream(pCompressedData, compressedData.Length);
            using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);

            int bytesRead;
            do
            {
                var buffer = uncompressedData.GetSpan();
                bytesRead = deflateStream.Read(buffer);
                uncompressedData.Advance(bytesRead);
            } while (bytesRead > 0);
        }
    }
}
