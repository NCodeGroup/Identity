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

namespace NCode.Jose.Compression;

/// <summary>
/// Provides methods for all compressions algorithms.
/// </summary>
public interface ICompressionAlgorithm : IAlgorithm
{
    /// <summary>
    /// When overridden in a derived class, compresses data.
    /// </summary>
    /// <param name="plainText">Contains the data to compress.</param>
    /// <param name="compressedPlainText">Destination for the compressed data.</param>
    void Compress(ReadOnlySpan<byte> plainText, Stream compressedPlainText);

    /// <summary>
    /// When overridden in a derived class, decompresses data.
    /// </summary>
    /// <param name="compressedPlainText">Contains the data to decompress.</param>
    /// <param name="plainText">Destination for the decompresses data.</param>
    void Decompress(ReadOnlySpan<byte> compressedPlainText, Stream plainText);
}

/// <summary>
/// Base implementation for all compression algorithms.
/// </summary>
public abstract class CompressionAlgorithm : Algorithm, ICompressionAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.Compression;

    /// <inheritdoc />
    public abstract void Compress(ReadOnlySpan<byte> plainText, Stream compressedPlainText);

    /// <inheritdoc />
    public abstract void Decompress(ReadOnlySpan<byte> compressedPlainText, Stream plainText);
}
