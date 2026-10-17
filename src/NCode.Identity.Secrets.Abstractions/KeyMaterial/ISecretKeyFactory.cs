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
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Provides factory methods to create <see cref="SecretKey"/> instances from cryptographic key material.
/// </summary>
/// <remarks>
/// This is a low-level convenience primitive for the <b>built-in</b> key families (symmetric, RSA, ECC); its typed
/// methods serve callers that already know which family they want. It is deliberately a closed set and is <b>not</b>
/// the extensibility seam — a custom key type is added by registering an <see cref="ISecretKeyTypeDescriptor"/> that
/// constructs its own <see cref="SecretKey"/> subtype, without routing through this factory.
/// </remarks>
[PublicAPI]
public interface ISecretKeyFactory
{
    /// <summary>
    /// Gets an empty <see cref="SecretKey"/> instance.
    /// </summary>
    SecretKey Empty { get; }

    /// <summary>
    /// Factory method to create <see cref="SymmetricSecretKey"/> instances from symmetric cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The symmetric cryptographic key material.</param>
    /// <returns>The newly created secret key.</returns>
    SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Factory method to create <see cref="SymmetricSecretKey"/> instances from symmetric cryptographic key material
    /// that is encoded as a UTF8 string (for example, a password).
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="password">The symmetric cryptographic key material that is encoded as a UTF8 string.</param>
    /// <returns>The newly created secret key.</returns>
    SymmetricSecretKey CreateSymmetric(KeyMetadata metadata, ReadOnlySpan<char> password);

    /// <summary>
    /// Factory method to create <see cref="AsymmetricSecretKey"/> instances from cryptographic key material in a <see cref="X509Certificate2"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="certificate">The <see cref="X509Certificate2"/> containing the cryptographic key material.</param>
    /// <returns>The newly created secret key.</returns>
    AsymmetricSecretKey Create(KeyMetadata metadata, X509Certificate2 certificate);

    /// <summary>
    /// Factory method to create a verify-only <see cref="RsaSecretKey"/> from the <b>public</b> half of an
    /// <c>RSA</c> key. The resulting key can verify signatures but not produce them, and holds no private material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="publicKey">The <c>RSA</c> public key material.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsaPublicKey(
        KeyMetadata metadata,
        RSA publicKey,
        X509Certificate2? certificate = null
    );

    /// <summary>
    /// Factory method to create a verify-only <see cref="RsaSecretKey"/> from the <b>public</b> half of an
    /// <c>RSA</c> key encoded as <c>SubjectPublicKeyInfo</c> (SPKI) DER. The resulting key can verify signatures but
    /// not produce them, and holds no private material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The <c>RSA</c> public key material encoded as <c>SubjectPublicKeyInfo</c> (SPKI) DER.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsaSpki(
        KeyMetadata metadata,
        ReadOnlySpan<byte> bytes,
        X509Certificate2? certificate = null
    );

    /// <summary>
    /// Factory method to create a verify-only <see cref="EccSecretKey"/> from the <b>public</b> half of an
    /// elliptic-curve key. The resulting key can verify <c>ECDSA</c> signatures but not produce them or derive
    /// shared secrets, and holds no private material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="publicKey">The elliptic-curve public key material.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEccPublicKey(
        KeyMetadata metadata,
        ECAlgorithm publicKey,
        X509Certificate2? certificate = null
    );

    /// <summary>
    /// Factory method to create a verify-only <see cref="EccSecretKey"/> from the <b>public</b> half of an
    /// elliptic-curve key encoded as <c>SubjectPublicKeyInfo</c> (SPKI) DER. The resulting key can verify
    /// <c>ECDSA</c> signatures but not produce them or derive shared secrets, and holds no private material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The elliptic-curve public key material encoded as <c>SubjectPublicKeyInfo</c> (SPKI) DER.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEccSpki(
        KeyMetadata metadata,
        ReadOnlySpan<byte> bytes,
        X509Certificate2? certificate = null
    );

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
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsaPem(
        KeyMetadata metadata,
        ReadOnlySpan<char> chars,
        X509Certificate2? certificate = null
    );

    /// <summary>
    /// Factory method to create <see cref="RsaSecretKey"/> instances from cryptographic key material using <c>PKCS#8</c> encoding.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The cryptographic key material using <c>PKCS#8</c> encoding.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    RsaSecretKey CreateRsaPkcs8(
        KeyMetadata metadata,
        ReadOnlySpan<byte> bytes,
        X509Certificate2? certificate = null
    );

    /// <summary>
    /// Factory method to create <see cref="EccSecretKey"/> instances from <c>ECAlgorithm</c> cryptographic key material.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="key">The <c>ECAlgorithm</c> cryptographic key material.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEcc(
        KeyMetadata metadata,
        ECAlgorithm key,
        X509Certificate2? certificate = null
    );

    /// <summary>
    /// Factory method to create <see cref="EccSecretKey"/> instances from cryptographic key material using <c>PEM</c> encoding.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="chars">The cryptographic key material using <c>PEM</c> encoding.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEccPem(
        KeyMetadata metadata,
        ReadOnlySpan<char> chars,
        X509Certificate2? certificate = null
    );

    /// <summary>
    /// Factory method to create <see cref="EccSecretKey"/> instances from cryptographic key material using <c>PKCS#8</c> encoding.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="bytes">The cryptographic key material using <c>PKCS#8</c> encoding.</param>
    /// <param name="certificate">The optional <see cref="X509Certificate2"/> for the secret key.</param>
    /// <returns>The newly created secret key.</returns>
    EccSecretKey CreateEccPkcs8(
        KeyMetadata metadata,
        ReadOnlySpan<byte> bytes,
        X509Certificate2? certificate = null
    );
}
