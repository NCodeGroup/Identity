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

using NIdentity.OpenId.Cryptography.Aead;
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.AesCbcHmac;

/// <summary>
/// Provides factory methods to create providers for <c>AES CBC HMAC SHA2</c> cryptographic algorithms.
/// </summary>
public class AesCbcHmacCryptoFactory : CryptoFactory<AesCbcHmacCryptoFactory, SharedSecretKey>
{
    /// <inheritdoc />
    protected override SharedSecretKey CoreGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default) =>
        SharedSecretKey.GenerateNewKey(descriptor, keyBitLengthHint);

    /// <inheritdoc />
    public override AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(
        SecretKey secretKey,
        AuthenticatedEncryptionAlgorithmDescriptor descriptor)
    {
        var typedSecretKey = ValidateSecretKey<SharedSecretKey>(secretKey);
        var typedDescriptor = ValidateDescriptor<AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor>(descriptor);

        return new AesCbcHmacAuthenticatedEncryptionProvider(typedSecretKey, typedDescriptor);
    }
}
