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
using NIdentity.OpenId.Cryptography.Ecc;

namespace NIdentity.OpenId.Cryptography.Ecdsa;

internal class EcdsaAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new HashSignatureAlgorithmDescriptor(
            EcdsaCryptoFactory.Default,
            typeof(EccSecretKey),
            AlgorithmCodes.DigitalSignature.EcdsaSha256,
            HashAlgorithmName.SHA256,
            HashBitLength: 256),

        new HashSignatureAlgorithmDescriptor(
            EcdsaCryptoFactory.Default,
            typeof(EccSecretKey),
            AlgorithmCodes.DigitalSignature.EcdsaSha384,
            HashAlgorithmName.SHA384,
            HashBitLength: 384),

        new HashSignatureAlgorithmDescriptor(
            EcdsaCryptoFactory.Default,
            typeof(EccSecretKey),
            AlgorithmCodes.DigitalSignature.EcdsaSha512,
            HashAlgorithmName.SHA512,
            HashBitLength: 512),
    };
}
