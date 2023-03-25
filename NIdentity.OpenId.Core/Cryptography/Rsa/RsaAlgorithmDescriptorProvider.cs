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

namespace NIdentity.OpenId.Cryptography.Rsa;

internal class RsaAlgorithmDescriptorProvider : IAlgorithmDescriptorProvider
{
    private RsaCryptoFactory CryptoFactory { get; } = new();

    public IEnumerable<AlgorithmDescriptor> Load() => new[]
    {
        new RsaSignatureAlgorithmDescriptor(CryptoFactory, AlgorithmCodes.DigitalSignature.RsaSha256, HashAlgorithmName.SHA256, 256, RSASignaturePadding.Pkcs1),
        new RsaSignatureAlgorithmDescriptor(CryptoFactory, AlgorithmCodes.DigitalSignature.RsaSha384, HashAlgorithmName.SHA384, 384, RSASignaturePadding.Pkcs1),
        new RsaSignatureAlgorithmDescriptor(CryptoFactory, AlgorithmCodes.DigitalSignature.RsaSha512, HashAlgorithmName.SHA512, 512, RSASignaturePadding.Pkcs1),
        new RsaSignatureAlgorithmDescriptor(CryptoFactory, AlgorithmCodes.DigitalSignature.RsaSsaPssSha256, HashAlgorithmName.SHA256, 256, RSASignaturePadding.Pss),
        new RsaSignatureAlgorithmDescriptor(CryptoFactory, AlgorithmCodes.DigitalSignature.RsaSsaPssSha384, HashAlgorithmName.SHA384, 384, RSASignaturePadding.Pss),
        new RsaSignatureAlgorithmDescriptor(CryptoFactory, AlgorithmCodes.DigitalSignature.RsaSsaPssSha512, HashAlgorithmName.SHA512, 512, RSASignaturePadding.Pss),
    };
}
