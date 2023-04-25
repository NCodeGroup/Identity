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

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Cryptography.Keys;

/// <summary>
/// Provides a <see cref="SecretKey"/> implementation for <c>RSA</c> cryptographic keys.
/// </summary>
public class RsaSecretKey : AsymmetricSecretKey
{
    /// <summary>
    /// OID for RSA public key cryptography.
    /// </summary>
    public const string Oid = "1.2.840.113549.1.1.1";

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaSecretKey"/> class with the specified <c>PKCS#8</c> key material
    /// and optional certificate.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="keySizeBits">The length of the key material in bits.</param>
    /// <param name="pkcs8PrivateKey">The bytes of the key material formatted as <c>PKCS#8</c>.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    public RsaSecretKey(string keyId, int keySizeBits, ReadOnlySpan<byte> pkcs8PrivateKey, X509Certificate2? certificate = null)
        : base(keyId, keySizeBits, pkcs8PrivateKey, certificate)
    {
        Debug.Assert(certificate == null || certificate.GetKeyAlgorithm() == Oid);
    }

    /// <summary>
    /// Factory method to create an <see cref="RSA"/> instance from the current <c>RSA</c> key material.
    /// </summary>
    /// <returns>The newly created <see cref="RSA"/> instance</returns>
    public RSA ExportRSA() => ExportAsymmetricAlgorithm(RSA.Create);
}
