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
using NIdentity.OpenId.Cryptography.CryptoProvider.Aead;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aead.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.AesCbcHmac.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.AesCbcHmac;

/// <summary>
/// Provides factory methods to create providers for <c>AES CBC HMAC SHA2</c> cryptographic algorithms.
/// </summary>
public class AesCbcHmacCryptoFactory : SymmetricCryptoFactory<AesCbcHmacCryptoFactory>
{
    /// <inheritdoc />
    public override AuthenticatedEncryptionProvider CreateAuthenticatedEncryptionProvider(
        SecretKey secretKey,
        AuthenticatedEncryptionAlgorithmDescriptor descriptor)
    {
        KeySizesUtility.AssertLegalSize(secretKey, descriptor);

        var typedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);
        var typedDescriptor = ValidateDescriptor<AesCbcHmacAuthenticatedEncryptionAlgorithmDescriptor>(descriptor);

        return new AesCbcHmacAuthenticatedEncryptionProvider(typedSecretKey, typedDescriptor);
    }
}
