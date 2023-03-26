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
    private SharedSecretKey SharedSecretKey { get; }
    private AesKeyWrapAlgorithmDescriptor Descriptor { get; }

    public AesKeyWrapProvider(IAesKeyWrap aesKeyWrap, SharedSecretKey secretKey, AesKeyWrapAlgorithmDescriptor descriptor) :
        base(secretKey, descriptor)
    {
        AesKeyWrap = aesKeyWrap;
        SharedSecretKey = secretKey;
        Descriptor = descriptor;
    }

    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        // TODO: validate shared key length early

        if (parameters is not ContentKeyWrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var keyByteLength = Descriptor.KeyByteLength;
        var kek = keyByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteLength] :
            new byte[keyByteLength];

        var bytesWritten = SharedSecretKey.GetKeyBytes(kek);
        if (bytesWritten != keyByteLength)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        return AesKeyWrap.WrapKey(
            kek,
            typedParameters,
            Descriptor.KeyBitLength);
    }

    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        // TODO: validate shared key length early

        if (parameters is not ContentKeyUnwrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var keyByteLength = Descriptor.KeyByteLength;
        var kek = keyByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteLength] :
            new byte[keyByteLength];

        var bytesWritten = SharedSecretKey.GetKeyBytes(kek);
        if (bytesWritten != keyByteLength)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        return AesKeyWrap.UnwrapKey(
            kek,
            typedParameters,
            Descriptor.KeyBitLength);
    }
}
