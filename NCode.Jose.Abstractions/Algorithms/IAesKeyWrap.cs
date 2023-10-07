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

namespace NCode.Jose.Algorithms;

/// <summary>
/// Defines cryptographic operations for the <c>AES</c> key wrap algorithm.
/// https://datatracker.ietf.org/doc/html/rfc3394
/// </summary>
public interface IAesKeyWrap
{
    /// <summary>
    /// Gets a collection of <see cref="KeySizes"/> that describe the valid sizes, in bytes, of the content encryption key (CEK).
    /// </summary>
    IEnumerable<KeySizes> LegalCekByteSizes { get; }

    /// <summary>
    /// Gets the size, in bytes, of the resulting ciphertext for <see cref="TryWrapKey"/>.
    /// </summary>
    /// <param name="contentKeySizeBytes">The size, in bytes, of the key encryption key (KEK).</param>
    /// <returns>The size, in bytes, of the resulting ciphertext for <see cref="TryWrapKey"/>.</returns>
    int GetEncryptedContentKeySizeBytes(int contentKeySizeBytes);

    /// <summary>
    /// Performs the cryptographic operation of encrypting key data using the AES key wrap algorithm.
    /// </summary>
    /// <param name="keyEncryptionKey">Contains the key encryption key (KEK).</param>
    /// <param name="contentKey">Contains the content encryption key (CEK) that is to be encrypted.</param>
    /// <param name="encryptedContentKey">Destination for result of encrypting the content key.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="encryptedContentKey"/>.</param>
    /// <returns><c>true</c> if <paramref name="encryptedContentKey"/> was large enough to receive the encrypted data; otherwise, <c>false</c>.</returns>
    bool TryWrapKey(
        ReadOnlySpan<byte> keyEncryptionKey,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten);

    /// <summary>
    /// Gets the size, in bytes, of the resulting plaintext for <see cref="TryUnwrapKey"/>.
    /// </summary>
    /// <param name="encryptedContentKeySizeBytes">The size, in bytes, of the encrypted key encryption key (KEK).</param>
    /// <returns>The size, in bytes, of the resulting plaintext for <see cref="TryUnwrapKey"/>.</returns>
    int GetContentKeySizeBytes(int encryptedContentKeySizeBytes);

    /// <summary>
    /// Performs the cryptographic operation of decrypting key data using the AES key wrap algorithm.
    /// </summary>
    /// <param name="keyEncryptionKey">Contains the key encryption key (KEK).</param>
    /// <param name="encryptedContentKey">Contains the encrypted content encryption key (CEK) that is to be decrypted.</param>
    /// <param name="contentKey">Destination for result of decrypting the encrypted content key.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="contentKey"/>.</param>
    /// <returns><c>true</c> if <paramref name="contentKey"/> was large enough to receive the decrypted data; otherwise, <c>false</c>.</returns>
    bool TryUnwrapKey(
        ReadOnlySpan<byte> keyEncryptionKey,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten);
}
