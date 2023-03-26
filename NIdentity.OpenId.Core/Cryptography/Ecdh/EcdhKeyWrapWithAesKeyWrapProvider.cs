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
using NIdentity.OpenId.Cryptography.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider;

namespace NIdentity.OpenId.Cryptography.Ecdh;

internal class EcdhKeyWrapWithAesKeyWrapProvider : EcdhKeyWrapProvider
{
    private IAesKeyWrap AesKeyWrap { get; }
    private EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor Descriptor { get; }

    public EcdhKeyWrapWithAesKeyWrapProvider(IAesKeyWrap aesKeyWrap, EcdhSecretKey secretKey, EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        AesKeyWrap = aesKeyWrap;
        Descriptor = descriptor;
    }

    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        var typedParameters = ValidateParameters<EcdhEsKeyWrapWithAesKeyWrapParameters>(parameters);

        using var ourPublicKey = EcdhSecretKey.Key.PublicKey;
        var keyAgreement = DeriveKey(typedParameters, typedParameters.RecipientKey, ourPublicKey);

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = keyAgreement.ToArray();

        return AesKeyWrap.WrapKey(aes, typedParameters, Descriptor.KeyBitLength);
    }

    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        var typedParameters = ValidateParameters<EcdhEsKeyUnwrapWithAesKeyUnwrapParameters>(parameters);

        var keyAgreement = DeriveKey(typedParameters, EcdhSecretKey.Key, typedParameters.SenderPublicKey);

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = keyAgreement.ToArray();

        return AesKeyWrap.UnwrapKey(aes, typedParameters, Descriptor.KeyBitLength);
    }
}
