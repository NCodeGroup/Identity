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
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.AesCbcHmac;

/// Provides an implementation of <see cref="IAlgorithmDescriptorProvider"/> that returns a collection of
/// descriptors for the <c>AES CBC HMAC SHA2</c> algorithm.
public class AesCbcHmacAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    /// <inheritdoc />
    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor(
            HMACSHA256.TryHashData,
            AesCbcHmacCryptoFactory.Default,
            AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,
            KeyBitLength: 128,
            HashBitLength: 256),

        new AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor(
            HMACSHA384.TryHashData,
            AesCbcHmacCryptoFactory.Default,
            AlgorithmCodes.AuthenticatedEncryption.Aes192CbcHmacSha384,
            KeyBitLength: 192,
            HashBitLength: 384),

        new AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor(
            HMACSHA512.TryHashData,
            AesCbcHmacCryptoFactory.Default,
            AlgorithmCodes.AuthenticatedEncryption.Aes256CbcHmacSha512,
            KeyBitLength: 256,
            HashBitLength: 512),
    };
}
