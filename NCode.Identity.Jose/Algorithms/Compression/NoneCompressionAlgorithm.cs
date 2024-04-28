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

namespace NCode.Identity.Jose.Algorithms.Compression;

/// <summary>
/// Provides an implementation of <see cref="CompressionAlgorithm"/> that does not perform compression and reads/writes data as-is.
/// </summary>
public class NoneCompressionAlgorithm : CompressionAlgorithm
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
    public override void Compress(ReadOnlySpan<byte> uncompressedData, IBufferWriter<byte> compressedData)
    {
        if (uncompressedData.IsEmpty) return;
        compressedData.Write(uncompressedData);
    }

    /// <inheritdoc />
    public override void Decompress(ReadOnlySpan<byte> compressedData, IBufferWriter<byte> uncompressedData)
    {
        if (compressedData.IsEmpty) return;
        uncompressedData.Write(compressedData);
    }
}
