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
/// Provides a <see cref="SecretKey"/> implementation for <c>Elliptic-Curve</c> cryptographic keys.
/// </summary>
/// <remarks>
/// Can be used for either <see cref="ECDsa"/> or <see cref="ECDiffieHellman"/> keys.
/// </remarks>
public class EccSecretKey : AsymmetricSecretKey
{
    /// <summary>
    /// OID for <c>Elliptic-Curve</c> public key cryptography.
    /// </summary>
    public const string Oid = "1.2.840.10045.2.1";

    /// <summary>
    /// Factory method to create an <see cref="EccSecretKey"/> from an <see cref="ECDsa"/> instance.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="tags">The collection of tags associated with the secret key.</param>
    /// <param name="key">An <see cref="ECDsa"/> that contains the ECC key material.</param>
    /// <returns>The newly created <see cref="EccSecretKey"/> instance.</returns>
    public static EccSecretKey Create(string keyId, IEnumerable<string> tags, ECDsa key) =>
        SecretKeyFactory.Create(key, bytes => new EccSecretKey(keyId, tags, key.KeySize, bytes));

    /// <summary>
    /// Factory method to create an <see cref="EccSecretKey"/> from an <see cref="ECDiffieHellman"/> instance.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="tags">The collection of tags associated with the secret key.</param>
    /// <param name="key">An <see cref="ECDiffieHellman"/> that contains the ECC key material.</param>
    /// <returns>The newly created <see cref="EccSecretKey"/> instance.</returns>
    public static EccSecretKey Create(string keyId, IEnumerable<string> tags, ECDiffieHellman key) =>
        SecretKeyFactory.Create(key, bytes => new EccSecretKey(keyId, tags, key.KeySize, bytes));

    /// <summary>
    /// Initializes a new instance of the <see cref="EccSecretKey"/> class with the specified <c>PKCS#8</c> key material
    /// and optional certificate.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="tags">The collection of tags associated with the secret key.</param>
    /// <param name="curveSizeBits">The size of the ECC curve in bits.</param>
    /// <param name="pkcs8PrivateKey">The bytes of the key material formatted as <c>PKCS#8</c>.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    public EccSecretKey(string keyId, IEnumerable<string> tags, int curveSizeBits, ReadOnlySpan<byte> pkcs8PrivateKey, X509Certificate2? certificate = null)
        : base(keyId, tags, curveSizeBits, pkcs8PrivateKey, certificate)
    {
        Debug.Assert(certificate == null || certificate.GetKeyAlgorithm() == Oid);
    }

    /// <summary>
    /// Gets the <see cref="ECCurve"/> for the current <c>Elliptic-Curve</c> key material.
    /// </summary>
    /// <returns>The <see cref="ECCurve"/> for the current <c>Elliptic-Curve</c> key material.</returns>
    public ECCurve GetECCurve() => KeySizeBits switch
    {
        256 => ECCurve.NamedCurves.nistP256,
        384 => ECCurve.NamedCurves.nistP384,
        521 => ECCurve.NamedCurves.nistP521,
        _ => GetCustomCurve()
    };

    private ECCurve GetCustomCurve()
    {
        using var key = ExportECDiffieHellman();
        var parameters = key.ExportParameters(includePrivateParameters: false);
        return parameters.Curve;
    }

    /// <summary>
    /// Factory method to create an <see cref="ECDsa"/> instance from the current <c>Elliptic-Curve</c> key material.
    /// </summary>
    /// <returns>The newly created <see cref="ECDsa"/> instance</returns>
    public ECDsa ExportECDsa() => ExportAsymmetricAlgorithm(ECDsa.Create);

    /// <summary>
    /// Factory method to create an <see cref="ECDiffieHellman"/> instance from the current <c>Elliptic-Curve</c> key material.
    /// </summary>
    /// <returns>The newly created <see cref="ECDiffieHellman"/> instance</returns>
    public ECDiffieHellman ExportECDiffieHellman() => ExportAsymmetricAlgorithm(ECDiffieHellman.Create);
}
