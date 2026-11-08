// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

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
    /// Factory method to create an <see cref="AsymmetricSecretKey"/> from a certificate collection, such as one loaded
    /// from a PKCS#12 file: the one certificate with a private key is the key's own certificate, and the others are
    /// the certificates of the authorities that issued it (<see cref="AsymmetricSecretKey.IssuerCertificatesRawData"/>).
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="certificates">The key's certificate, with its private key, and its issuers' certificates.</param>
    /// <returns>The newly created <see cref="AsymmetricSecretKey"/> instance.</returns>
    /// <exception cref="ArgumentException">The collection does not hold exactly one certificate with a private
    /// key.</exception>
    // See ADR-1300.
    AsymmetricSecretKey Create(KeyMetadata metadata, X509Certificate2Collection certificates);

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

    /// <summary>
    /// Factory method to create an <see cref="RsaSecretKey"/> whose private half a key management service holds, from
    /// the key's stored <c>SubjectPublicKeyInfo</c> (SPKI) DER. The resulting key is an <see cref="IRemoteSecretKey"/>
    /// that verifies like any public <c>RSA</c> key and signs through its <see cref="RemoteKeyBinding"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="subjectPublicKeyInfo">The <c>RSA</c> public key material encoded as <c>SubjectPublicKeyInfo</c> (SPKI) DER.</param>
    /// <param name="binding">The binding of the key to its provider, reference and mapping row.</param>
    /// <returns>The newly created secret key.</returns>
    /// <exception cref="ArgumentException">The binding's mapping row is not for <c>RSA</c> keys, or its key size
    /// differs from the size of the public key.</exception>
    // See ADR-1471.
    RsaSecretKey CreateRsaRemote(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        RemoteKeyBinding binding
    );

    /// <summary>
    /// Factory method to create an <see cref="EccSecretKey"/> whose private half a key management service holds, from
    /// the key's stored <c>SubjectPublicKeyInfo</c> (SPKI) DER. The resulting key is an <see cref="IRemoteSecretKey"/>
    /// that verifies <c>ECDSA</c> signatures like any public elliptic-curve key and signs through its
    /// <see cref="RemoteKeyBinding"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="KeyMetadata"/> for the secret key.</param>
    /// <param name="subjectPublicKeyInfo">The elliptic-curve public key material encoded as <c>SubjectPublicKeyInfo</c> (SPKI) DER.</param>
    /// <param name="binding">The binding of the key to its provider, reference and mapping row.</param>
    /// <returns>The newly created secret key.</returns>
    /// <exception cref="ArgumentException">The binding's mapping row is not for elliptic-curve keys, or its key size
    /// or curve differs from the public key's.</exception>
    // See ADR-1471.
    EccSecretKey CreateEccRemote(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        RemoteKeyBinding binding
    );
}
