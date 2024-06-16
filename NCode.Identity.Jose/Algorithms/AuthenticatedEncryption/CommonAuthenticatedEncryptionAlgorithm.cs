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
using JetBrains.Annotations;
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;

/// <summary>
/// Provides common implementation for all cryptographic authenticated encryption (AEAD) algorithms.
/// </summary>
[PublicAPI]
public abstract class CommonAuthenticatedEncryptionAlgorithm : AuthenticatedEncryptionAlgorithm
{
    private IEnumerable<KeySizes>? KeyBitSizesOrNull { get; set; }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => KeyBitSizesOrNull ??= new[]
    {
        new KeySizes(minSize: ContentKeySizeBits, maxSize: ContentKeySizeBits, skipSize: 0)
    };

    /// <summary>
    /// Gets the size, in bits, of the content encryption key (CEK) that is supported by this authenticated encryption (AEAD) algorithm.
    /// </summary>
    protected internal virtual int ContentKeySizeBits => ContentKeySizeBytes << 3;

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
    protected internal virtual void ValidateParameters(
        bool encrypt,
        ReadOnlySpan<byte> cek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plainText,
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> authenticationTag)
    {
        if (!KeySizesUtility.IsLegalSize(KeyBitSizes, cek.Length << 3))
            throw new ArgumentException("The specified content encryption key (CEK) does not have a valid size for this cryptographic algorithm.", nameof(cek));

        if (nonce.Length != NonceSizeBytes)
            throw new ArgumentException("The specified nonce does not have a valid size for this cryptographic algorithm.", nameof(nonce));

        if (encrypt && cipherText.Length != GetCipherTextSizeBytes(plainText.Length))
            throw new ArgumentException("The specified plain text and cipher text do not have a valid size for this cryptographic algorithm.", nameof(cipherText));

        if (authenticationTag.Length != AuthenticationTagSizeBytes)
            throw new ArgumentException("The specified authentication tag does not have a valid size for this cryptographic algorithm.", nameof(authenticationTag));
    }
}
