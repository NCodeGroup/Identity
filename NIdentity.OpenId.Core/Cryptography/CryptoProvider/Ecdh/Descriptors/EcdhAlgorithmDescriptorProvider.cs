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

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Descriptors;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDescriptorProvider"/> that returns a collection of
/// descriptors for the <c>ECDH</c> algorithm.
/// </summary>
public class EcdhAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    /// <inheritdoc />
    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new EcdhKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.EcdhEs,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            HashSizeBits: 256),

        new EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.EcdhEsAes128,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            HashSizeBits: 256,
            KeySizeBits: 128),

        new EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.EcdhEsAes192,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            HashSizeBits: 256,
            KeySizeBits: 192),

        new EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.EcdhEsAes256,
            KeyDerivationFunctionTypes.SP800_56A_CONCAT,
            HashAlgorithmName.SHA256,
            HashSizeBits: 256,
            KeySizeBits: 256),
    };
}
