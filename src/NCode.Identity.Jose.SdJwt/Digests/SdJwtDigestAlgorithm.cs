// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Jose.SdJwt.Digests;

/// <summary>
/// A hash algorithm that a Selective Disclosure JWT (SD-JWT) names in its <c>_sd_alg</c> claim (RFC 9901 §4.1.1): the
/// algorithm's name from the IANA Named Information Hash Algorithm Registry and the platform hash that computes it.
/// The registered algorithms are an open vocabulary: an application registers another instance beside the built-in
/// ones.
/// </summary>
// See ADR-1405.
[PublicAPI]
public sealed class SdJwtDigestAlgorithm
{
    private const int StackAllocLimit = 256;

    /// <summary>
    /// Gets the name of the algorithm as the <c>_sd_alg</c> claim carries it, such as <c>sha-256</c>, compared
    /// ordinally.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the platform hash algorithm that computes the digests.
    /// </summary>
    public required HashAlgorithmName HashAlgorithmName { get; init; }

    /// <summary>
    /// Gets the size of a digest, in bytes.
    /// </summary>
    public int DigestSizeBytes => HashAlgorithmName.GetHashSizeBits() / 8;

    /// <summary>
    /// Computes the base64url-encoded digest of the ASCII octets of a value (RFC 9901 §4.2.3): the digest of a
    /// disclosure is computed over its base64url encoding, and the <c>sd_hash</c> of a Key Binding JWT over the
    /// presentation it accompanies.
    /// </summary>
    /// <param name="value">The disclosure or presentation to hash. It must be ASCII.</param>
    /// <returns>The base64url-encoded digest.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> contains a character that is not ASCII.</exception>
    public string ComputeDigest(ReadOnlySpan<char> value)
    {
        var hashFunction = HashAlgorithmName.GetHashFunction();
        var digestSizeBytes = DigestSizeBytes;

        Span<byte> digest =
            digestSizeBytes <= StackAllocLimit
                ? stackalloc byte[digestSizeBytes]
                : new byte[digestSizeBytes];

        IDisposable? lease = null;
        try
        {
            // ReSharper disable once UseCollectionExpression
            Span<byte> source = stackalloc byte[0];

            if (value.Length <= StackAllocLimit)
            {
                source = stackalloc byte[StackAllocLimit];
            }
            else
            {
                lease = BufferFactory.Rent(value.Length, isSensitive: false, out source);
            }

            var status = Ascii.FromUtf16(value, source, out var written);
            if (status != OperationStatus.Done)
                throw new ArgumentException(
                    "The value must contain only ASCII characters.",
                    nameof(value)
                );

            if (
                !hashFunction(source[..written], digest, out var bytesWritten)
                || bytesWritten != digestSizeBytes
            )
                throw new InvalidOperationException(
                    "The hash function did not produce the expected number of bytes."
                );
        }
        finally
        {
            lease?.Dispose();
        }

        return Base64Url.Encode(digest);
    }
}
