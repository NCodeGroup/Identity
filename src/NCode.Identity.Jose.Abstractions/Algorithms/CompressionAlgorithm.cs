// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides methods for all compressions algorithms.
/// </summary>
[PublicAPI]
public abstract class CompressionAlgorithm : Algorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.Compression;

    /// <summary>
    /// When overridden in a derived class, compresses data.
    /// </summary>
    /// <param name="uncompressedData">Contains the data to compress.</param>
    /// <param name="compressedData">Destination for the compressed data.</param>
    public abstract void Compress(
        ReadOnlySpan<byte> uncompressedData,
        IBufferWriter<byte> compressedData
    );

    /// <summary>
    /// When overridden in a derived class, decompresses data.
    /// </summary>
    /// <param name="compressedData">Contains the data to decompress.</param>
    /// <param name="uncompressedData">Destination for the uncompressed data.</param>
    public abstract void Decompress(
        ReadOnlySpan<byte> compressedData,
        IBufferWriter<byte> uncompressedData
    );
}
