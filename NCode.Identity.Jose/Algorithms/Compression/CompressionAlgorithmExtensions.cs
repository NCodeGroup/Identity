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
using NCode.CryptoMemory;
using NCode.Disposables;
using Nerdbank.Streams;

namespace NCode.Identity.Jose.Algorithms.Compression;

internal static class CompressionAlgorithmExtensions
{
    public static IDisposable Compress(
        this CompressionAlgorithm? algorithm,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> uncompressedData,
        out ReadOnlySpan<byte> compressedData)
    {
        if (algorithm == null || algorithm.Code == AlgorithmCodes.Compression.None)
        {
            compressedData = uncompressedData;
            return Disposable.Empty;
        }

        var buffer = new Sequence<byte>
        {
            // increase our chances of getting a single-segment buffer
            MinimumSpanLength = Math.Min(uncompressedData.Length, 1024)
        };

        try
        {
            algorithm.Compress(uncompressedData, buffer);
            header[JoseClaimNames.Header.Zip] = algorithm.Code;

            var sequence = buffer.AsReadOnlySequence;
            if (sequence.IsSingleSegment)
            {
                compressedData = sequence.First.Span;
                return buffer;
            }

            var byteCount = (int)sequence.Length;
            var lease = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> span);
            try
            {
                sequence.CopyTo(span);
                buffer.Dispose();
                compressedData = span;
            }
            catch
            {
                lease.Dispose();
                throw;
            }

            return lease;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }
}
