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

namespace NIdentity.OpenId.Cryptography.Keys;

/// <summary>
/// Provides a <see cref="SecretKey"/> implementation for the <c>RSA</c> cryptographic keys.
/// </summary>
public class RsaSecretKey : AsymmetricSecretKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RsaSecretKey"/> class with the specified PKCS#8 key material.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="keyBitLength">The length of the key material in bits.</param>
    /// <param name="pkcs8PrivateKey">The bytes of the key material formatted as PKCS#8.</param>
    public RsaSecretKey(string keyId, int keyBitLength, ReadOnlySpan<byte> pkcs8PrivateKey)
        : base(keyId, keyBitLength, pkcs8PrivateKey)
    {
        // nothing
    }

    /// <summary>
    /// Factory method to create an <see cref="RSA"/> instance from the current PKCS#8 key material.
    /// </summary>
    /// <returns>The newly created <see cref="RSA"/> instance</returns>
    public RSA CreateRsa() => CreateAsymmetricAlgorithm(RSA.Create);
}
