// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Nerdbank.Streams;

namespace NCode.Identity.Jose.Algorithms.Compression;

/// <summary>
/// Provides an implementation of <see cref="CompressionAlgorithm"/> that uses the <c>DEFLATE (RFC1951)</c> algorithm for compression.
/// </summary>
internal class DeflateCompressionAlgorithm : CompressionAlgorithm
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
    public override void Compress(
        ReadOnlySpan<byte> uncompressedData,
        IBufferWriter<byte> compressedData
    )
    {
        if (uncompressedData.IsEmpty)
            return;

        using var compressedStream = compressedData.AsStream();
        using var deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal);

        deflateStream.Write(uncompressedData);
    }

    /// <inheritdoc />
    public override unsafe void Decompress(
        ReadOnlySpan<byte> compressedData,
        IBufferWriter<byte> uncompressedData
    )
    {
        if (compressedData.IsEmpty)
            return;

        // perf: use GetReference vs GetPinnableReference
        // https://github.com/dotnet/runtime/issues/27308
        // https://github.com/dotnet/runtime/issues/29003

        fixed (byte* pCompressedData = &MemoryMarshal.GetReference(compressedData))
        {
            using var compressedStream = new UnmanagedMemoryStream(
                pCompressedData,
                compressedData.Length
            );
            using var deflateStream = new DeflateStream(
                compressedStream,
                CompressionMode.Decompress
            );

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
