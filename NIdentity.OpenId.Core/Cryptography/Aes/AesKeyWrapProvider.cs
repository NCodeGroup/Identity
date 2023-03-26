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
using NIdentity.OpenId.Cryptography.CryptoProvider;

namespace NIdentity.OpenId.Cryptography.Aes;

internal class AesKeyWrapProvider : KeyWrapProvider
{
    private IAesKeyWrap AesKeyWrap { get; }
    private AesSecretKey AesSecretKey { get; }
    private AesKeyWrapAlgorithmDescriptor Descriptor { get; }

    public AesKeyWrapProvider(IAesKeyWrap aesKeyWrap, AesSecretKey secretKey, AesKeyWrapAlgorithmDescriptor descriptor) :
        base(secretKey, descriptor)
    {
        AesKeyWrap = aesKeyWrap;
        AesSecretKey = secretKey;
        Descriptor = descriptor;
    }

    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        if (parameters is not ContentKeyWrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        return AesKeyWrap.WrapKey(
            AesSecretKey.Key,
            typedParameters,
            Descriptor.KeyBitLength);
    }

    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        if (parameters is not ContentKeyUnwrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        return AesKeyWrap.UnwrapKey(
            AesSecretKey.Key,
            typedParameters,
            Descriptor.KeyBitLength);
    }
}
