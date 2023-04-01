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
using NIdentity.OpenId.Cryptography.CryptoProvider.Aes.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Parameters;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Aes;

/// <summary>
/// Provides an implementation of <see cref="KeyWrapProvider"/> using the <c>Advanced Encryption Standard (AES) Key Wrap Algorithm</c>.
/// https://datatracker.ietf.org/doc/html/rfc3394
/// </summary>
public class AesKeyWrapProvider : KeyWrapProvider
{
    private IAesKeyWrap AesKeyWrap { get; }

    /// <summary>
    /// Gets the <see cref="SharedSecretKey"/> containing the key material used by the <c>AES</c> algorithm.
    /// </summary>
    public SharedSecretKey SharedSecretKey { get; }

    private AesKeyWrapAlgorithmDescriptor Descriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesKeyWrapProvider"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Contains the <see cref="IAesKeyWrap"/> instance that implements the actual AES key wrap algorithm.</param>
    /// <param name="secretKey">Contains the <c>key encryption key (kek)</c> for the AES key wrap algorithm.</param>
    /// <param name="descriptor">Contains the <see cref="AesKeyWrapAlgorithmDescriptor"/> the describes the AES key wrap algorithm.</param>
    public AesKeyWrapProvider(IAesKeyWrap aesKeyWrap, SharedSecretKey secretKey, AesKeyWrapAlgorithmDescriptor descriptor) :
        base(secretKey, descriptor)
    {
        AesKeyWrap = aesKeyWrap;
        SharedSecretKey = secretKey;
        Descriptor = descriptor;
    }

    /// <inheritdoc />
    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        // TODO: validate shared key length early

        if (parameters is not ContentKeyWrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        return AesKeyWrap.WrapKey(
            SharedSecretKey.KeyBytes,
            typedParameters,
            Descriptor.KeyBitLength);
    }

    /// <inheritdoc />
    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        // TODO: validate shared key length early

        if (parameters is not ContentKeyUnwrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        return AesKeyWrap.UnwrapKey(
            SharedSecretKey.KeyBytes,
            typedParameters,
            Descriptor.KeyBitLength);
    }
}
