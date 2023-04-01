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
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Aes.Descriptors;

public record AesKeyWrapAlgorithmDescriptor(
    string AlgorithmCode,
    int KeyBitLength
) : KeyWrapAlgorithmDescriptor
(
    AesCryptoFactory.Default,
    typeof(SharedSecretKey),
    AlgorithmCode
), ISupportLegalSizes
{
    /// <inheritdoc />
    public IEnumerable<KeySizes> LegalSizes { get; } = new[]
    {
        new KeySizes(minSize: KeyBitLength, maxSize: KeyBitLength, skipSize: 0)
    };

    /// <summary>
    /// Gets the number of bytes for the <c>key encryption key (kek)</c>.
    /// </summary>
    public int KeyByteLength => KeyBitLength / BinaryUtility.BitsPerByte;
}