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
using System.Text.Json;
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides methods for all cryptographic key management algorithms.
/// </summary>
public abstract class KeyManagementAlgorithm : KeyedAlgorithm
{
    /// <inheritdoc />
    public override AlgorithmType Type => AlgorithmType.KeyManagement;

    /// <summary>
    /// Gets the legal key sizes, in bytes, of the content encryption key (CEK)
    /// given the specified size, in bits, of the key encryption key (KEK).
    /// </summary>
    /// <param name="kekSizeBits">The size, int bits, of the key encryption key (KEK).</param>
    /// <returns>The legal sizes, in bytes, of the content encryption key (CEK).</returns>
    public abstract IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits);

    /// <summary>
    /// Gets the expected size, in bytes, of the encrypted content encryption key (CEK) given
    /// both the size, in bits, of the key encryption key (KEK)
    /// and the size, in bytes, of the content encryption key (CEK).
    /// </summary>
    /// <param name="kekSizeBits">The size, in bits, of the key encryption key (KEK).</param>
    /// <param name="cekSizeBytes">The size, in bytes, of the content encryption key (CEK).</param>
    /// <returns>The size, in bytes, of the encrypted content encryption key (CEK).</returns>
    public abstract int GetEncryptedContentKeySizeBytes(
        int kekSizeBits,
        int cekSizeBytes);

    /// <summary>
    /// Generates a new content encryption key (CEK).
    /// </summary>
    /// <param name="secretKey">The key encryption key (KEK), for the current cryptographic algorithm.</param>
    /// <param name="header">The JOSE header for the current cryptographic operation.</param>
    /// <param name="contentKey">Destination for the new content encryption key (CEK).</param>
    public abstract void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey);

    /// <summary>
    /// Performs the cryptographic operation of encrypting a content encryption key (CEK) with an key encryption key (KEK).
    /// </summary>
    /// <param name="secretKey">The key encryption key (KEK), for the current cryptographic algorithm.</param>
    /// <param name="header">The JOSE header for the current cryptographic operation.</param>
    /// <param name="contentKey">The content encryption key (CEK) to encrypt.</param>
    /// <param name="encryptedContentKey">The destination for the encrypted content encryption key (CEK).</param>
    /// <param name="bytesWritten">When this method returns, contains a value that indicates the number of bytes written to <paramref name="encryptedContentKey"/>.</param>
    /// <returns><c>true</c> if <paramref name="encryptedContentKey"/> is big enough to receive the output; otherwise, <c>false</c>.</returns>
    public abstract bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten);

    /// <summary>
    /// Performs the cryptographic operation of encrypting a newly generated content encryption key (CEK) with an key encryption key (KEK).
    /// </summary>
    /// <param name="secretKey">The key encryption key (KEK), for the current cryptographic algorithm.</param>
    /// <param name="header">The JOSE header for the current cryptographic operation.</param>
    /// <param name="contentKey">The destination for the newly generated content encryption key (CEK).</param>
    /// <param name="encryptedContentKey">The destination for the encrypted content encryption key (CEK).</param>
    /// <param name="bytesWritten">When this method returns, contains a value that indicates the number of bytes written to <paramref name="encryptedContentKey"/>.</param>
    /// <returns><c>true</c> if <paramref name="encryptedContentKey"/> is big enough to receive the output; otherwise, <c>false</c>.</returns>
    public abstract bool TryWrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten);

    /// <summary>
    /// Performs the cryptographic operation of decrypting an encrypted content encryption key (CEK) with an key encryption key (KEK).
    /// </summary>
    /// <param name="secretKey">The key encryption key (KEK), for the current cryptographic algorithm.</param>
    /// <param name="header">The JOSE header for the current cryptographic operation.</param>
    /// <param name="encryptedContentKey">The encrypted content encryption key (CEK) to decrypt.</param>
    /// <param name="contentKey">The destination for the decrypted content encryption key (CEK).</param>
    /// <param name="bytesWritten">When this method returns, contains a value that indicates the number of bytes written to <paramref name="contentKey"/>.</param>
    /// <returns><c>true</c> if <paramref name="encryptedContentKey"/> is big enough to receive the output; otherwise, <c>false</c>.</returns>
    public abstract bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten);
}
