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

public abstract record AuthenticatedEncryptionAlgorithmDescriptor
(
    ICryptoFactory CryptoFactory,
    string AlgorithmCode,
    int KeyBitLength
) : AlgorithmDescriptor
(
    CryptoFactory,
    AlgorithmTypes.AuthenticatedEncryption,
    AlgorithmCode
)
{
    /// <summary>
    /// Gets the number of bytes for the <c>key encryption key (kek)</c>.
    /// </summary>
    public int KeyByteLength => KeyBitLength / BinaryUtility.BitsPerByte;

    /// <summary>
    /// Gets the length of a ciphertext with a given plaintext length.
    /// </summary>
    /// <param name="plainTextSizeBytes">The plaintext length, in bytes.</param>
    /// <returns>The length, in bytes, of the ciphertext.</returns>
    public abstract int GetCipherTextLength(int plainTextSizeBytes);
}
