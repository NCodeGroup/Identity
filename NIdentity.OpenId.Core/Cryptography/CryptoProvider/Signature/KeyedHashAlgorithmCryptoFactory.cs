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
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Signature;

/// <summary>
/// Provides factory methods to create providers for various keyed hash cryptographic algorithms.
/// </summary>
public class KeyedHashAlgorithmCryptoFactory : SymmetricCryptoFactory<KeyedHashAlgorithmCryptoFactory>
{
    /// <inheritdoc />
    public override SignatureProvider CreateSignatureProvider(
        SecretKey secretKey,
        SignatureAlgorithmDescriptor descriptor)
    {
        KeySizesUtility.AssertLegalSize(secretKey, descriptor);

        var typedSecretKey = ValidateSecretKey<SharedSecretKey>(secretKey);
        var typedDescriptor = ValidateDescriptor<KeyedHashAlgorithmDescriptor>(descriptor);

        return new KeyedHashAlgorithmSignatureProvider(typedSecretKey, typedDescriptor);
    }
}
