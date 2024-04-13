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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides factory methods to create <see cref="SecretKey"/> instances from cryptographic key material.
/// </summary>
public interface ISecretKeyFactory
{
    /// <summary>
    /// Factory method to create <see cref="AsymmetricSecretKey"/> instances from cryptographic key material in a <see cref="X509Certificate2"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="certificate">The <see cref="X509Certificate2"/> containing the cryptographic key material.</param>
    /// <returns>The newly created secret key.</returns>
    AsymmetricSecretKey Create(KeyMetadata metadata, X509Certificate2 certificate);

    /// <summary>
    /// Factory method to create <see cref="RsaSecretKey"/> instances from <c>PKCS#8</c> encoded cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="modulusSizeBits">The size of the RSA modulus in bits.</param>
    /// <param name="pkcs8PrivateKeyBytes">The cryptographic key material using <c>PKCS#8</c> encoding.</param>
    /// <param name="pkcs8PrivateKeySizeBytes">The length of the <c>PKCS#8</c> key material in bytes.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsa(
        KeyMetadata metadata,
        int modulusSizeBits,
        IMemoryOwner<byte> pkcs8PrivateKeyBytes,
        int pkcs8PrivateKeySizeBytes,
        X509Certificate2? certificate = null);

    /// <summary>
    /// Factory method to create <see cref="RsaSecretKey"/> instances from <c>RSA</c> cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="key">The <c>RSA</c> cryptographic key material.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsa(KeyMetadata metadata, RSA key, X509Certificate2? certificate = null);

    /// <summary>
    /// Factory method to create <see cref="RsaSecretKey"/> instances from cryptographic key material using <c>PEM</c> encoding.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="chars">The cryptographic key material using <c>PEM</c> encoding.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsaPem(KeyMetadata metadata, ReadOnlySpan<char> chars);

    /// <summary>
    /// Factory method to create <see cref="RsaSecretKey"/> instances from cryptographic key material using <c>PKCS#8</c> encoding.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The cryptographic key material using <c>PKCS#8</c> encoding.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsaPkcs8(KeyMetadata metadata, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Factory method to create <see cref="EccSecretKey"/> instances from <c>PKCS#8</c> encoded cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="curveSizeBits">The size of the ECC curve in bits.</param>
    /// <param name="pkcs8PrivateKeyBytes">The cryptographic key material using <c>PKCS#8</c> encoding.</param>
    /// <param name="pkcs8PrivateKeySizeBytes">The length of the <c>PKCS#8</c> key material in bytes.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEcc(
        KeyMetadata metadata,
        int curveSizeBits,
        IMemoryOwner<byte> pkcs8PrivateKeyBytes,
        int pkcs8PrivateKeySizeBytes,
        X509Certificate2? certificate = null);

    /// <summary>
    /// Factory method to create <see cref="EccSecretKey"/> instances from <c>ECAlgorithm</c> cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="key">The <c>ECAlgorithm</c> cryptographic key material.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEcc(KeyMetadata metadata, ECAlgorithm key, X509Certificate2? certificate = null);

    /// <summary>
    /// Factory method to create <see cref="EccSecretKey"/> instances from cryptographic key material using <c>PEM</c> encoding.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="chars">The cryptographic key material using <c>PEM</c> encoding.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEccPem(KeyMetadata metadata, ReadOnlySpan<char> chars);

    /// <summary>
    /// Factory method to create <see cref="EccSecretKey"/> instances from cryptographic key material using <c>PKCS#8</c> encoding.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The cryptographic key material using <c>PKCS#8</c> encoding.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEccPkcs8(KeyMetadata metadata, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Factory method to create <see cref="SymmetricSecretKey"/> instances from symmetric cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The symmetric cryptographic key material.</param>
    /// <param name="byteCount">The size of the cryptographic key material in bytes.</param>
    /// <returns>The newly created secret key.</returns>
    SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, IMemoryOwner<byte> bytes, int byteCount);

    /// <summary>
    /// Factory method to create <see cref="SymmetricSecretKey"/> instances from symmetric cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The symmetric cryptographic key material.</param>
    /// <returns>The newly created secret key.</returns>
    SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Factory method to create <see cref="SymmetricSecretKey"/> instances from symmetric cryptographic key material
    /// that is encoded as a UTF8 string (aka password).
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="password">The symmetric cryptographic key material that is encoded as a UTF8 string.</param>
    /// <returns>The newly created secret key.</returns>
    SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<char> password);
}
