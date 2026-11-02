// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>key encryption key (KEK)</c> directly for key agreement.
/// </summary>
internal class DirectKeyManagementAlgorithm : CommonKeyManagementAlgorithm
{
    /// <summary>
    /// Gets a singleton instance of <see cref="DirectKeyManagementAlgorithm"/>.
    /// </summary>
    public static DirectKeyManagementAlgorithm Singleton { get; } = new();

    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } =
    [new(minSize: 8, maxSize: int.MaxValue, skipSize: 8)];

    private static IReadOnlyList<AlgorithmKeyType> StaticKeyTypes { get; } =
    [new(typeof(SymmetricSecretKey), StaticKeyBitSizes)];

    private static IEnumerable<KeySizes> StaticCekByteSizes { get; } =
    [new(minSize: 1, maxSize: int.MaxValue, skipSize: 1)];

    /// <inheritdoc />
    public override string Code => AlgorithmCodes.KeyManagement.Direct;

    /// <inheritdoc />
    public override bool SupportsKeyWrapping => false;

    /// <inheritdoc />
    public override IReadOnlyList<AlgorithmKeyType> KeyTypes => StaticKeyTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectKeyManagementAlgorithm"/> class.
    /// </summary>
    private DirectKeyManagementAlgorithm()
    {
        // nothing
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        StaticCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) => 0;

    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey
    )
    {
        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(StaticKeyBitSizes);
        if (contentKey.Length != validatedSecretKey.KeySizeBytes)
        {
            throw new JoseException(
                "The size of the destination buffer for CEK must identical to the KEK size."
            );
        }

        var privateKeyWriter = contentKey.GetFixedBufferWriter();
        validatedSecretKey.ExportPrivateKey(ref privateKeyWriter);
        Debug.Assert(privateKeyWriter.WrittenCount == validatedSecretKey.KeySizeBytes);
    }

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        throw new JoseException(
            "The direct key management algorithm does not support using an existing CEK."
        );
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
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten
    )
    {
        if (encryptedContentKey.Length != 0)
        {
            throw new JoseException(
                "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm."
            );
        }

        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(StaticKeyBitSizes);
        if (contentKey.Length < validatedSecretKey.KeySizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        var contentKeyWriter = contentKey.GetFixedBufferWriter();

        validatedSecretKey.ExportPrivateKey(ref contentKeyWriter);

        bytesWritten = contentKeyWriter.WrittenCount;
        return true;
    }
}
