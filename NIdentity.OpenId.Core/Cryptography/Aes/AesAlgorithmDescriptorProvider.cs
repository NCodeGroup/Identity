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

using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.Aes;

internal class AesAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    private ICryptoFactory CryptoFactory { get; } = new AesCryptoFactory();

    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new AesKeyWrapAlgorithmDescriptor(
            CryptoFactory,
            AlgorithmCodes.KeyManagement.Aes128,
            KeyBitLength: 128),

        new AesKeyWrapAlgorithmDescriptor(
            CryptoFactory,
            AlgorithmCodes.KeyManagement.Aes192,
            KeyBitLength: 192),

        new AesKeyWrapAlgorithmDescriptor(
            CryptoFactory,
            AlgorithmCodes.KeyManagement.Aes256,
            KeyBitLength: 256),
    };
}
