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

using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.Rsa.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Cryptography.Keys.Material;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Rsa;

/// <summary>
/// Provides factory methods to create providers for <c>RSA</c> cryptographic algorithms.
/// </summary>
public class RsaCryptoFactory : CryptoFactory<RsaCryptoFactory>
{
    /// <inheritdoc />
    public override Type SecretKeyType => typeof(RsaSecretKey);

    /// <inheritdoc />
    protected override KeyMaterial GenerateKeyMaterial(int keySizeBits) =>
        new AsymmetricKeyMaterial(RSA.Create(keySizeBits), owns: true);

    /// <inheritdoc />
    protected override RsaSecretKey CreateSecretKey(string keyId, int keySizeBits, ReadOnlySpan<byte> keyMaterial) =>
        new(keyId, keySizeBits, keyMaterial);

    /// <inheritdoc />
    public override SignatureProvider CreateSignatureProvider(
        SecretKey secretKey,
        SignatureAlgorithmDescriptor descriptor)
    {
        KeySizesUtility.AssertLegalSize(secretKey, descriptor);

        var typedSecretKey = ValidateSecretKey<RsaSecretKey>(secretKey);
        var typedDescriptor = ValidateDescriptor<RsaSignatureAlgorithmDescriptor>(descriptor);

        return new RsaSignatureProvider(typedSecretKey, typedDescriptor);
    }
}
