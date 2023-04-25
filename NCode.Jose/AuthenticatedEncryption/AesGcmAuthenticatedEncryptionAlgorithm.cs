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
using NCode.Cryptography.Keys;
using NCode.Jose.Exceptions;

namespace NCode.Jose.AuthenticatedEncryption;

/// <summary>
/// Provides an implementation of <see cref="AuthenticatedEncryptionAlgorithm"/> that uses the <c>AES GCM</c> cryptographic algorithm for authenticated encryption (AEAD).
/// </summary>
public class AesGcmAuthenticatedEncryptionAlgorithm : AuthenticatedEncryptionAlgorithm
{
    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KekBitSizes { get; }

    /// <inheritdoc />
    public override KeySizes NonceByteSizes => AesGcm.NonceByteSizes;

    /// <inheritdoc />
    public override KeySizes AuthenticationTagByteSizes => AesCcm.TagByteSizes;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmAuthenticatedEncryptionAlgorithm"/> class.
    /// </summary>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="kekSizeBits">Contains the legal size, in bits, of the key encryption key (KEK).</param>
    public AesGcmAuthenticatedEncryptionAlgorithm(string code, int kekSizeBits)
    {
        Code = code;

        KekBitSizes = new[] { new KeySizes(minSize: kekSizeBits, maxSize: kekSizeBits, skipSize: 0) };
    }

    /// <inheritdoc />
    public override int GetCipherTextSizeBytes(int plainTextSizeBytes)
        => plainTextSizeBytes;

    /// <inheritdoc />
    public override int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes)
        => cipherTextSizeBytes;

    private static AesGcm CreateAesGcm(SymmetricSecretKey secretKey) =>
        new(secretKey.KeyBytes);

    /// <inheritdoc />
    public override void Encrypt(
        SecretKey secretKey,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag)
    {
        var validatedSecretKey = ValidateParameters(
            encrypt: true,
            secretKey,
            nonce,
            plainText,
            cipherText,
            authenticationTag);

        using var key = CreateAesGcm(validatedSecretKey);

        key.Encrypt(nonce, plainText, cipherText, authenticationTag, associatedData);
    }

    /// <inheritdoc />
    public override bool TryDecrypt(
        SecretKey secretKey,
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

        var validatedSecretKey = ValidateParameters(
            encrypt: false,
            secretKey,
            nonce,
            plainText,
            cipherText,
            authenticationTag);

        using var key = CreateAesGcm(validatedSecretKey);
        try
        {
            key.Decrypt(nonce, cipherText, authenticationTag, plainText, associatedData);
        }
        catch (CryptographicException exception)
        {
            throw new EncryptionException("Failed to decrypt ciphertext.", exception);
        }

        bytesWritten = cipherText.Length;
        return true;
    }
}
