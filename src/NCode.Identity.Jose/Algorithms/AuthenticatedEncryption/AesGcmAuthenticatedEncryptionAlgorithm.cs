// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;

/// <summary>
/// Provides an implementation of <see cref="AuthenticatedEncryptionAlgorithm"/> that uses the <c>AES GCM</c> cryptographic algorithm for authenticated encryption (AEAD).
/// </summary>
/// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
/// <param name="cekSizeBits">Contains the legal size, in bits, of the content encryption key (CEK).</param>
internal class AesGcmAuthenticatedEncryptionAlgorithm(string code, int cekSizeBits)
    : CommonAuthenticatedEncryptionAlgorithm
{
    /// <inheritdoc />
    public override string Code { get; } = code;

    /// <inheritdoc />
    public override int ContentKeySizeBytes { get; } = (cekSizeBits + 7) >> 3;

    /// <inheritdoc />
    public override int NonceSizeBytes => AesGcm.NonceByteSizes.MinSize;

    /// <inheritdoc />
    public override int AuthenticationTagSizeBytes => AesCcm.TagByteSizes.MaxSize;

    /// <inheritdoc />
    public override int GetCipherTextSizeBytes(int plainTextSizeBytes) => plainTextSizeBytes;

    /// <inheritdoc />
    public override int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes) => cipherTextSizeBytes;

    /// <inheritdoc />
    public override void Encrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag
    )
    {
        ValidateParameters(encrypt: true, cek, nonce, plainText, cipherText, authenticationTag);

        using var key = new AesGcm(cek, AuthenticationTagSizeBytes);

        key.Encrypt(nonce, plainText, cipherText, authenticationTag, associatedData);
    }

    /// <inheritdoc />
    public override bool TryDecrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten
    )
    {
        if (plainText.Length != cipherText.Length)
        {
            bytesWritten = 0;
            return false;
        }

        ValidateParameters(encrypt: false, cek, nonce, plainText, cipherText, authenticationTag);

        using var key = new AesGcm(cek, AuthenticationTagSizeBytes);
        try
        {
            key.Decrypt(nonce, cipherText, authenticationTag, plainText, associatedData);
        }
        catch (CryptographicException exception)
        {
            throw new JoseEncryptionException("Failed to decrypt the ciphertext.", exception);
        }

        bytesWritten = cipherText.Length;
        return true;
    }
}
