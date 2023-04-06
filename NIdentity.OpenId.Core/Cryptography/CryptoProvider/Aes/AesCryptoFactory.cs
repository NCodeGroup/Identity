﻿#region Copyright Preamble

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
using NIdentity.OpenId.Cryptography.CryptoProvider.Aes.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Aes;

/// <summary>
/// Provides factory methods to create providers for <c>AES</c> cryptographic algorithms.
/// </summary>
public class AesCryptoFactory : CryptoFactory<AesCryptoFactory, SharedSecretKey>
{
    /// <inheritdoc />
    protected override SharedSecretKey CoreGenerateNewKey(string keyId, AlgorithmDescriptor descriptor, int? keyBitLengthHint = default) =>
        SharedSecretKey.GenerateNewKey(keyId, descriptor, keyBitLengthHint);

    /// <inheritdoc />
    public override KeyWrapProvider CreateKeyWrapProvider(
        SecretKey secretKey,
        KeyWrapAlgorithmDescriptor descriptor)
    {
        KeySizesUtility.AssertLegalSize(secretKey, descriptor);

        var typedSecretKey = ValidateSecretKey<SharedSecretKey>(secretKey);
        var typedDescriptor = ValidateDescriptor<AesKeyWrapAlgorithmDescriptor>(descriptor);

        return new AesKeyWrapProvider(AesKeyWrap.Default, typedSecretKey, typedDescriptor);
    }
}
