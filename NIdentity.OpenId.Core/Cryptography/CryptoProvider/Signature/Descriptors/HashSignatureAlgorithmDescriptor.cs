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

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;

public record HashSignatureAlgorithmDescriptor
(
    ICryptoFactory CryptoFactory,
    Type SecretKeyType,
    string AlgorithmCode,
    HashAlgorithmName HashAlgorithmName,
    int HashSizeBits
) : SignatureAlgorithmDescriptor
(
    CryptoFactory,
    SecretKeyType,
    AlgorithmCode,
    HashSizeBits
);
