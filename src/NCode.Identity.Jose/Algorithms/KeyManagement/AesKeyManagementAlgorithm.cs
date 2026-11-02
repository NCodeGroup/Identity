// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>AES</c> cryptographic algorithm for key management.
/// </summary>
/// <param name="aesKeyWrap">Provides the AES key wrap functionality.</param>
/// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
/// <param name="kekSizeBits">Contains the legal size, in bits, of the key encryption key (KEK).</param>
internal class AesKeyManagementAlgorithm(IAesKeyWrap aesKeyWrap, string code, int kekSizeBits)
    : CommonKeyManagementAlgorithm
{
    private IAesKeyWrap AesKeyWrap { get; } = aesKeyWrap;

    /// <inheritdoc />
    public override string Code { get; } = code;

    /// <inheritdoc />
    public override Type KeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes { get; } =
    [new KeySizes(minSize: kekSizeBits, maxSize: kekSizeBits, skipSize: 0)];

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        AesKeyWrap.LegalCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        AesKeyWrap.GetEncryptedContentKeySizeBytes(cekSizeBytes);

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(KeyBitSizes);

        // increase our chances for a single-segment buffer
        using var privateKeyBuffer = BufferFactory.CreatePooledBufferWriter(
            isSensitive: true,
            secretKey.KeySizeBytes
        );
        IBufferWriter<byte> privateKeyWriter = privateKeyBuffer;

        validatedSecretKey.ExportPrivateKey(ref privateKeyWriter);
        Debug.Assert(privateKeyBuffer.Length == validatedSecretKey.KeySizeBytes);

        using var privateKeySpanLease = privateKeyBuffer.GetSpanLease(isSensitive: true);
        var privateKeySpan = privateKeySpanLease.Span;

        AesKeyWrap.WrapKey(privateKeySpan, contentKey, ref encryptedContentKeyWriter);
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
        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(KeyBitSizes);

        if (contentKey.Length < AesKeyWrap.GetContentKeySizeBytes(encryptedContentKey.Length))
        {
            bytesWritten = 0;
            return false;
        }

        // increase our chances for a single-segment buffer
        using var privateKeyBuffer = BufferFactory.CreatePooledBufferWriter(
            true,
            secretKey.KeySizeBytes
        );
        IBufferWriter<byte> privateKeyWriter = privateKeyBuffer;

        validatedSecretKey.ExportPrivateKey(ref privateKeyWriter);
        Debug.Assert(privateKeyBuffer.Length == validatedSecretKey.KeySizeBytes);

        using var privateKeySpanLease = privateKeyBuffer.GetSpanLease(isSensitive: true);
        var privateKeySpan = privateKeySpanLease.Span;

        var contentKeyWriter = contentKey.GetFixedBufferWriter();

        AesKeyWrap.UnwrapKey(privateKeySpan, encryptedContentKey, ref contentKeyWriter);

        bytesWritten = contentKeyWriter.WrittenCount;
        return true;
    }
}
