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
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.Hmac;

internal class HmacAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new KeyedHashAlgorithmDescriptor(
            HMACSHA256.TryHashData,
            KeyedHashAlgorithmCryptoFactory.Default,
            AlgorithmCodes.DigitalSignature.HmacSha256,
            HashBitLength: 256),

        new KeyedHashAlgorithmDescriptor(
            HMACSHA384.TryHashData,
            KeyedHashAlgorithmCryptoFactory.Default,
            AlgorithmCodes.DigitalSignature.HmacSha384,
            HashBitLength: 384),

        new KeyedHashAlgorithmDescriptor(
            HMACSHA512.TryHashData,
            KeyedHashAlgorithmCryptoFactory.Default,
            AlgorithmCodes.DigitalSignature.HmacSha512,
            HashBitLength: 512),
    };
}
