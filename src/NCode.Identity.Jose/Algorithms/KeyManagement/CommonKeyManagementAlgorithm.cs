// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Base implementation for all cryptographic key management algorithms.
/// </summary>
internal abstract class CommonKeyManagementAlgorithm : KeyManagementAlgorithm
{
    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey
    )
    {
        ValidateContentKeySize(secretKey.KeySizeBits, contentKey.Length);
        RandomNumberGenerator.Fill(contentKey);
    }

    /// <inheritdoc />
    public override void WrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        NewKey(secretKey, header, contentKey);

        WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter);
    }

    /// <summary>
    /// Asserts that the size of the content encryption key (CEK) is valid for this cryptographic algorithm.
    /// </summary>
    /// <param name="kekSizeBits">The size, in bits, of the key encryption key (KEK).</param>
    /// <param name="cekSizeBytes">The size, in bytes, of the content encryption key (CEK).</param>
    /// <exception cref="JoseException">Thrown when the size of the content encryption key (CEK) is invalid.</exception>
    protected internal void ValidateContentKeySize(int kekSizeBits, int cekSizeBytes)
    {
        var legalCekByteSizes = GetLegalCekByteSizes(kekSizeBits);
        if (!KeySizesUtility.IsLegalSize(legalCekByteSizes, cekSizeBytes))
        {
            throw new JoseException(
                "The content encryption key (CEK) does not have a valid size for this cryptographic algorithm."
            );
        }
    }
}
