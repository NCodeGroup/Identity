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

namespace NCode.Jose.AuthenticatedEncryption;

/// <summary>
/// Provides methods for all cryptographic authenticated encryption (AEAD) algorithms.
/// </summary>
public interface IAuthenticatedEncryptionAlgorithm : IKeyedAlgorithm
{
    /// <summary>
    /// Gets the size, in bytes, of the content encryption key (CEK) that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    int ContentKeySizeBytes { get; }

    /// <summary>
    /// Gets the nonce sizes, in bytes, that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    KeySizes NonceByteSizes { get; }

    /// <summary>
    /// Gets the tag sizes, in bytes, that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    KeySizes AuthenticationTagByteSizes { get; }

    /// <summary>
    /// Gets the size, in bytes, of a ciphertext with a given plaintext size.
    /// </summary>
    /// <param name="plainTextSizeBytes">The plaintext size, in bytes.</param>
    /// <returns>The size, in bytes, of the ciphertext.</returns>
    int GetCipherTextSizeBytes(int plainTextSizeBytes);

    /// <summary>
    /// Gets the maximum size, in bytes, of a plaintext with a given ciphertext size.
    /// </summary>
    /// <param name="cipherTextSizeBytes">The ciphertext size, in bytes.</param>
    /// <returns>The maximum size, in bytes, of the plaintext.</returns>
    int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes);

    /// <summary>
    /// When overridden in a derived class, encrypts the plaintext into the ciphertext destination buffer
    /// and generates the authentication tag into a separate buffer.
    /// </summary>
    /// <param name="cek">Contains the content encryption key (CEK).</param>
    /// <param name="nonce">The nonce associated with this message, which should be a unique value for every operation with the same key.</param>
    /// <param name="plainText">The content to encrypt.</param>
    /// <param name="associatedData">Extra data associated with this message, which must also be provided during decryption.</param>
    /// <param name="cipherText">The byte array to receive the encrypted contents.</param>
    /// <param name="authenticationTag">The byte array to receive the generated authentication tag.</param>
    void Encrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag);

    /// <summary>
    /// When overridden in a derived class, decrypts the ciphertext into the provided destination buffer if the authentication tag can be validated.
    /// </summary>
    /// <param name="cek">Contains the content encryption key (CEK).</param>
    /// <param name="nonce">The nonce associated with this message, which must match the value provided during encryption.</param>
    /// <param name="cipherText">The encrypted content to decrypt.</param>
    /// <param name="associatedData">Extra data associated with this message, which must match the value provided during encryption.</param>
    /// <param name="authenticationTag">The authentication tag produced for this message during encryption.</param>
    /// <param name="plainText">The byte array to receive the decrypted contents.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="plainText"/>.</param>
    /// <returns><c>true</c> if <paramref name="plainText"/> was large enough to receive the decrypted data; otherwise, <c>false</c>.</returns>
    bool TryDecrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten);
}

/// <summary>
/// Base implementation for all cryptographic authenticated encryption (AEAD) algorithms.
/// </summary>
public abstract class AuthenticatedEncryptionAlgorithm : KeyedAlgorithm, IAuthenticatedEncryptionAlgorithm
{
    private IEnumerable<KeySizes>? KeyBitSizesOrNull { get; set; }

    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.AuthenticatedEncryption;

    /// <inheritdoc />
    public override Type KeyType => typeof(ReadOnlySpan<byte>);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => KeyBitSizesOrNull ??= new[]
    {
        new KeySizes(minSize: ContentKeySizeBits, maxSize: ContentKeySizeBits, skipSize: 0)
    };

    /// <inheritdoc />
    public abstract int ContentKeySizeBytes { get; }

    /// <summary>
    /// Gets the size, in bits, of the content encryption key (CEK) that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    protected virtual int ContentKeySizeBits => ContentKeySizeBytes << 3;

    /// <inheritdoc />
    public abstract KeySizes NonceByteSizes { get; }

    /// <inheritdoc />
    public abstract KeySizes AuthenticationTagByteSizes { get; }

    /// <inheritdoc />
    public abstract int GetCipherTextSizeBytes(int plainTextSizeBytes);

    /// <inheritdoc />
    public abstract int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes);

    /// <inheritdoc />
    public abstract void Encrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> associatedData,
        Span<byte> cipherText,
        Span<byte> authenticationTag);

    /// <inheritdoc />
    public abstract bool TryDecrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten);

    /// <summary>
    /// Asserts that the specified parameters are valid according to their corresponding legal sizes.
    /// </summary>
    /// <param name="cek">Contains the content encryption key (CEK).</param>
    /// <param name="encrypt"><c>true</c> if the current operation is encryption; otherwise, <c>false</c> if the current operation is decryption.</param>
    /// <param name="nonce">The nonce associated with this message, which should be a unique value for every operation with the same key.</param>
    /// <param name="plainText">The content to encrypt.</param>
    /// <param name="cipherText">The byte array to receive the encrypted contents.</param>
    /// <param name="authenticationTag">The byte array to receive the generated authentication tag.</param>
    /// <exception cref="ArgumentException">Thrown when any of the validations fail.</exception>
    protected void ValidateParameters(
        bool encrypt,
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> authenticationTag)
    {
        if (!KeySizesUtility.IsLegalSize(KeyBitSizes, cek.Length << 3))
            throw new ArgumentException("The specified content encryption key (CEK) does not have a valid size for this cryptographic algorithm.", nameof(nonce));

        if (!KeySizesUtility.IsLegalSize(NonceByteSizes, nonce.Length))
            throw new ArgumentException("The specified nonce does not have a valid size for this cryptographic algorithm.", nameof(nonce));

        if (encrypt && cipherText.Length != GetCipherTextSizeBytes(plainText.Length))
            throw new ArgumentException("The specified plain text and cipher text do not have a valid size for this cryptographic algorithm.", nameof(nonce));

        if (!KeySizesUtility.IsLegalSize(AuthenticationTagByteSizes, authenticationTag.Length))
            throw new ArgumentException("The specified authentication tag does not have a valid size for this cryptographic algorithm.", nameof(authenticationTag));
    }
}
