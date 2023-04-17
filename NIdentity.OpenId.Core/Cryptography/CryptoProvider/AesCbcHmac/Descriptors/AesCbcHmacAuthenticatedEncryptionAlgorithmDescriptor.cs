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

using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aead.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.AesCbcHmac.Descriptors;

public record AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor
(
    KeyedHashFunctionDelegate KeyedHashFunction,
    string AlgorithmCode,
    int KeySizeBits,
    int HashSizeBits
) : AuthenticatedEncryptionAlgorithmDescriptor
(
    AesCbcHmacCryptoFactory.Default,
    typeof(SymmetricSecretKey),
    AlgorithmCode,
    KeySizeBits
)
{
    /// <summary>
    /// Contains the block size, in bits, of the cryptographic operation.
    /// </summary>
    public const int BlockSizeBits = 128;

    /// <summary>
    /// Gets the number of bytes for the <c>HMAC</c> hash.
    /// </summary>
    public int HashSizeBytes => HashSizeBits / BinaryUtility.BitsPerByte;

    /// <inheritdoc />
    public override int GetCipherTextLength(int plainTextSizeBytes)
    {
        const int blockSizeBytes = BlockSizeBits / BinaryUtility.BitsPerByte;
        var wholeBlocks = blockSizeBytes * plainTextSizeBytes / blockSizeBytes;
        return wholeBlocks + blockSizeBytes;
    }
}
