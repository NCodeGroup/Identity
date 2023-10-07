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
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Algorithms.KeyManagement;

/// <summary>
/// Base implementation for all cryptographic key management algorithms.
/// </summary>
public abstract class CommonKeyManagementAlgorithm : KeyManagementAlgorithm
{
    /// <inheritdoc />
    public override void NewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey)
    {
        ValidateContentKeySize(
            secretKey.KeySizeBits,
            contentKey.Length);

        RandomNumberGenerator.Fill(contentKey);
    }

    /// <inheritdoc />
    public override bool TryWrapNewKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        NewKey(
            secretKey,
            header,
            contentKey);

        return TryWrapKey(
            secretKey,
            header,
            contentKey,
            encryptedContentKey,
            out bytesWritten);
    }

    /// <summary>
    /// Asserts that the size of the content encryption key (CEK) is valid for this cryptographic algorithm.
    /// </summary>
    /// <param name="kekSizeBits">The size, in bits, of the key encryption key (KEK).</param>
    /// <param name="cekSizeBytes">The size, in bytes, of the content encryption key (CEK).</param>
    /// <exception cref="JoseException">Thrown when the size of the content encryption key (CEK) is invalid.</exception>
    protected internal void ValidateContentKeySize(int kekSizeBits, int cekSizeBytes)
    {
        var legalCekByteSizes = GetLegalCekByteSizes(kekSizeBits);
        if (!KeySizesUtility.IsLegalSize(legalCekByteSizes, cekSizeBytes))
        {
            throw new JoseException("The content encryption key (CEK) does not have a valid size for this cryptographic algorithm.");
        }
    }
}
