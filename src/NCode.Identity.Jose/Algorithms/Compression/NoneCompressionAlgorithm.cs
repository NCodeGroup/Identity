// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace NCode.Identity.Jose.Algorithms.Compression;

/// <summary>
/// Provides an implementation of <see cref="CompressionAlgorithm"/> that does not perform compression and reads/writes data as-is.
/// </summary>
internal class NoneCompressionAlgorithm : CompressionAlgorithm
{
    /// <summary>
    /// Gets a singleton instance of <see cref="NoneCompressionAlgorithm"/>.
    /// </summary>
    public static NoneCompressionAlgorithm Singleton { get; } = new();

    /// <inheritdoc />
    public override string Code => AlgorithmCodes.Compression.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoneCompressionAlgorithm"/> class.
    /// </summary>
    private NoneCompressionAlgorithm()
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
        compressedData.Write(uncompressedData);
    }

    /// <inheritdoc />
    public override void Decompress(
        ReadOnlySpan<byte> compressedData,
        IBufferWriter<byte> uncompressedData
    )
    {
        if (compressedData.IsEmpty)
            return;
        uncompressedData.Write(compressedData);
    }
}
