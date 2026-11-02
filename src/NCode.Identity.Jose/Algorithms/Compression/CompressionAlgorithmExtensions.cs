// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using NCode.Buffers;
using NCode.Disposables;
using Nerdbank.Streams;

namespace NCode.Identity.Jose.Algorithms.Compression;

internal static class CompressionAlgorithmExtensions
{
    extension(CompressionAlgorithm? algorithm)
    {
        public IDisposable Compress(
            IDictionary<string, object> header,
            ReadOnlySpan<byte> uncompressedData,
            out ReadOnlySpan<byte> compressedData
        )
        {
            if (algorithm == null || algorithm.Code == AlgorithmCodes.Compression.None)
            {
                compressedData = uncompressedData;
                return Disposable.Empty;
            }

            var buffer = new Sequence<byte>(ArrayPool<byte>.Shared)
            {
                // increase our chances of getting a single-segment buffer
                MinimumSpanLength = Math.Min(uncompressedData.Length, 1024),
            };

            try
            {
                algorithm.Compress(uncompressedData, buffer);
                header[JoseClaimNames.Header.Zip] = algorithm.Code;

                return buffer.ConsumeAsContiguousSpan(isSensitive: false, out compressedData);
            }
            catch
            {
                buffer.Dispose();
                throw;
            }
        }
    }
}
