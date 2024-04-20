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

using System.Security.Cryptography;
using NCode.Jose.Exceptions;

namespace NCode.Jose.Algorithms.AuthenticatedEncryption;

/// <summary>
/// Provides an implementation of <see cref="AuthenticatedEncryptionAlgorithm"/> that uses the <c>AES GCM</c> cryptographic algorithm for authenticated encryption (AEAD).
/// </summary>
public class AesGcmAuthenticatedEncryptionAlgorithm : CommonAuthenticatedEncryptionAlgorithm
{
    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override int ContentKeySizeBytes { get; }

    /// <inheritdoc />
    public override int NonceSizeBytes => AesGcm.NonceByteSizes.MinSize;

    /// <inheritdoc />
    public override int AuthenticationTagSizeBytes => AesCcm.TagByteSizes.MaxSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmAuthenticatedEncryptionAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="cekSizeBits">Contains the legal size, in bits, of the content encryption key (CEK).</param>
    public AesGcmAuthenticatedEncryptionAlgorithm(string code, int cekSizeBits)
    {
        Code = code;
        ContentKeySizeBytes = (cekSizeBits + 7) >> 3;
    }

    /// <inheritdoc />
    public override int GetCipherTextSizeBytes(int plainTextSizeBytes)
        => plainTextSizeBytes;

    /// <inheritdoc />
    public override int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes)
        => cipherTextSizeBytes;

    /// <inheritdoc />
    public override void Encrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag)
    {
        ValidateParameters(
            encrypt: true,
            cek,
            nonce,
            plainText,
            cipherText,
            authenticationTag);

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
        out int bytesWritten)
    {
        if (plainText.Length != cipherText.Length)
        {
            bytesWritten = 0;
            return false;
        }

        ValidateParameters(
            encrypt: false,
            cek,
            nonce,
            plainText,
            cipherText,
            authenticationTag);

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
