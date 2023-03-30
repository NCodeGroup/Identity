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

namespace NIdentity.OpenId.Cryptography.Descriptors;

public record SignatureAlgorithmDescriptor
(
    ICryptoFactory CryptoFactory,
    Type SecretKeyType,
    string AlgorithmCode,
    int HashBitLength
) : AlgorithmDescriptor
(
    CryptoFactory,
    SecretKeyType,
    AlgorithmTypes.DigitalSignature,
    AlgorithmCode
)
{
    /// <summary>
    /// Gets the number of bytes for the <c>digital signature hash</c>.
    /// </summary>
    public int HashByteLength => HashBitLength / BinaryUtility.BitsPerByte;
}
