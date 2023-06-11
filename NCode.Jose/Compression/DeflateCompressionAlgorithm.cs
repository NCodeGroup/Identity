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

using System.IO.Compression;

namespace NCode.Jose.Compression;

/// <summary>
/// Provides an implementation of <see cref="CompressionAlgorithm"/> that uses the <c>DEFLATE (RFC1951)</c> algorithm for compression.
/// </summary>
public class DeflateCompressionAlgorithm : CompressionAlgorithm
{
    /// <inheritdoc />
    public override string Code => AlgorithmCodes.Compression.Deflate;

    /// <inheritdoc />
    public override void Compress(ReadOnlySpan<byte> plainText, Stream compressedPlainText)
    {
        using var gzip = new DeflateStream(compressedPlainText, CompressionLevel.Optimal);

        gzip.Write(plainText);
    }

    /// <inheritdoc />
    public override unsafe void Decompress(ReadOnlySpan<byte> compressedPlainText, Stream plainText)
    {
        fixed (byte* pCompressedPlainText = &compressedPlainText[0])
        {
            using var stream = new UnmanagedMemoryStream(pCompressedPlainText, compressedPlainText.Length);
            using var deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
            deflateStream.CopyTo(plainText);
        }
    }
}
