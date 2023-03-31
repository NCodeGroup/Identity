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

using NIdentity.OpenId.Cryptography.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.Ecc;
using NIdentity.OpenId.Cryptography.KeyWrap;

namespace NIdentity.OpenId.Cryptography.Ecdh;

/// <summary>
/// Provides factory methods to create providers for <c>ECDH</c> cryptographic algorithms.
/// </summary>
public class EcdhCryptoFactory : CryptoFactory<EcdhCryptoFactory>
{
    /// <inheritdoc />
    public override KeyWrapProvider CreateKeyWrapProvider(
        SecretKey secretKey,
        KeyWrapAlgorithmDescriptor descriptor)
    {
        var typedSecretKey = ValidateSecretKey<EccSecretKey>(secretKey);
        var typedDescriptor = ValidateDescriptor<EcdhKeyWrapAlgorithmDescriptor>(descriptor);

        if (typedDescriptor is EcdhKeyWrapWithAesKeyWrapAlgorithmDescriptor withAesDescriptor)
        {
            return new EcdhKeyWrapWithAesKeyWrapProvider(
                AesKeyWrap.Default,
                typedSecretKey,
                withAesDescriptor);
        }

        return new EcdhKeyWrapProvider(typedSecretKey, typedDescriptor);
    }
}
