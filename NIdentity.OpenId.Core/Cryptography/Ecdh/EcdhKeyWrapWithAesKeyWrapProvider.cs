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

using System.Buffers;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Ecc;

namespace NIdentity.OpenId.Cryptography.Ecdh;

internal class EcdhKeyWrapWithAesKeyWrapProvider : EcdhKeyWrapProvider
{
    private IAesKeyWrap AesKeyWrap { get; }
    private EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor Descriptor { get; }

    public EcdhKeyWrapWithAesKeyWrapProvider(IAesKeyWrap aesKeyWrap, EccSecretKey secretKey, EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        AesKeyWrap = aesKeyWrap;
        Descriptor = descriptor;
    }

    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        // TODO: can be move other validations to be earlier too? (i.e. AesKeyWrap)

        var typedParameters = ValidateParameters<EcdhEsKeyWrapWithAesKeyWrapParameters>(parameters);

        using var ourPrivateKey = EccSecretKey.CreateECDiffieHellman();
        using var ourPublicKey = ourPrivateKey.PublicKey;
        var keyAgreement = DeriveKey(typedParameters, typedParameters.RecipientKey, ourPublicKey);

        var keyByteLength = Descriptor.KeyByteLength;
        if (keyAgreement.Length != keyByteLength)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var kek = keyByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteLength] :
            GC.AllocateUninitializedArray<byte>(keyByteLength, pinned: true);

        keyAgreement.CopyTo(kek);

        try
        {
            return AesKeyWrap.WrapKey(kek, typedParameters, Descriptor.KeyBitLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        // TODO: can be move other validations to be earlier too? (i.e. AesKeyWrap)

        var typedParameters = ValidateParameters<EcdhEsKeyUnwrapWithAesKeyUnwrapParameters>(parameters);

        using var ourPrivateKey = EccSecretKey.CreateECDiffieHellman();
        var keyAgreement = DeriveKey(typedParameters, ourPrivateKey, typedParameters.SenderPublicKey);

        var keyByteLength = Descriptor.KeyByteLength;
        if (keyAgreement.Length != keyByteLength)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var kek = keyByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteLength] :
            GC.AllocateUninitializedArray<byte>(keyByteLength, pinned: true);

        keyAgreement.CopyTo(kek);

        try
        {
            return AesKeyWrap.UnwrapKey(kek, typedParameters, Descriptor.KeyBitLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }
}
