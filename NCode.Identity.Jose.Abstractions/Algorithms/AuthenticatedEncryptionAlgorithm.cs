﻿#region Copyright Preamble

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

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides methods for all cryptographic authenticated encryption (AEAD) algorithms.
/// </summary>
[PublicAPI]
public abstract class AuthenticatedEncryptionAlgorithm : KeyedAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.AuthenticatedEncryption;

    /// <inheritdoc />
    public override Type KeyType => typeof(ReadOnlySpan<byte>);

    /// <summary>
    /// Gets the size, in bytes, of the content encryption key (CEK) that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    public abstract int ContentKeySizeBytes { get; }

    /// <summary>
    /// Gets the size, in bytes, of the nonce (aka IV) that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    public abstract int NonceSizeBytes { get; }

    /// <summary>
    /// Gets the size, in bytes, of the authentication tag that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    public abstract int AuthenticationTagSizeBytes { get; }

    /// <summary>
    /// Gets the size, in bytes, of a ciphertext with a given plaintext size.
    /// </summary>
    /// <param name="plainTextSizeBytes">The plaintext size, in bytes.</param>
    /// <returns>The size, in bytes, of the ciphertext.</returns>
    public abstract int GetCipherTextSizeBytes(int plainTextSizeBytes);

    /// <summary>
    /// Gets the maximum size, in bytes, of a plaintext with a given ciphertext size.
    /// </summary>
    /// <param name="cipherTextSizeBytes">The ciphertext size, in bytes.</param>
    /// <returns>The maximum size, in bytes, of the plaintext.</returns>
    public abstract int GetMaxPlainTextSizeBytes(int cipherTextSizeBytes);

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
    public abstract void Encrypt(
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
    public abstract bool TryDecrypt(
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> authenticationTag,
        Span<byte> plainText,
        out int bytesWritten);
}
