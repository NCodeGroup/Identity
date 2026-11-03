// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Security.Cryptography;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Identity.Jose.Algorithms.Compression;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jose.Algorithms.Signature;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="Algorithm"/> instances supported by this library.
/// </summary>
/// <param name="aesKeyWrap">Provides the common implementation for <c>AES Key Wrap</c> functionality.</param>
/// <param name="secretKeyTypeCatalog">The registered key types, whose descriptors write and read the ephemeral public key
/// of the <c>ECDH-ES</c> algorithms.</param>
/// <param name="ecdhKeyAgreements">The registered <c>ECDH-ES</c> key agreements, each over the curves of one key type
/// the <c>ECDH-ES</c> algorithms accept.</param>
/// <param name="eccSignatureAlgorithms">The registered <c>ECDSA</c> signature algorithms, each served beside the built-in
/// <c>ES256</c>, <c>ES384</c> and <c>ES512</c>.</param>
internal sealed class DefaultAlgorithmDataSource(
    IAesKeyWrap aesKeyWrap,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IEnumerable<IEcdhKeyAgreement> ecdhKeyAgreements,
    IEnumerable<EccSignatureAlgorithmSpecification> eccSignatureAlgorithms
) : ICollectionDataSource<Algorithm>
{
    // RFC 7518 §3.4 binds ES256, ES384 and ES512 to P-256, P-384 and P-521 (ADR-1272).
    private static IEnumerable<EccSignatureAlgorithmSpecification> BuiltInEccSignatureAlgorithms =>
        [
            new()
            {
                Code = AlgorithmCodes.DigitalSignature.EcdsaSha256,
                Curve = new EccCurveSpecification
                {
                    CurveName = "P-256",
                    CurveSizeBits = 256,
                    Curve = ECCurve.NamedCurves.nistP256,
                },
                HashAlgorithmName = HashAlgorithmName.SHA256,
            },
            new()
            {
                Code = AlgorithmCodes.DigitalSignature.EcdsaSha384,
                Curve = new EccCurveSpecification
                {
                    CurveName = "P-384",
                    CurveSizeBits = 384,
                    Curve = ECCurve.NamedCurves.nistP384,
                },
                HashAlgorithmName = HashAlgorithmName.SHA384,
            },
            new()
            {
                Code = AlgorithmCodes.DigitalSignature.EcdsaSha512,
                Curve = new EccCurveSpecification
                {
                    CurveName = "P-521",
                    CurveSizeBits = 521,
                    Curve = ECCurve.NamedCurves.nistP521,
                },
                HashAlgorithmName = HashAlgorithmName.SHA512,
            },
        ];

    // See ADR-1392: the built-in ECDSA algorithms and every registered one, validated as a whole.
    private ImmutableArray<EccSignatureAlgorithmSpecification> EccSignatureAlgorithms { get; } =
        ComposeEccSignatureAlgorithms(eccSignatureAlgorithms);

    private IAesKeyWrap AesKeyWrap { get; } = aesKeyWrap;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private IReadOnlyList<IEcdhKeyAgreement> EcdhKeyAgreements { get; } = [.. ecdhKeyAgreements];

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;

    /// <inheritdoc />
    public IEnumerable<Algorithm> Collection
    {
        get
        {
            #region Signature Algorithms

            // The unsecured `none` algorithm (RFC 7518 §3.6) is not implemented: it proves nothing (ADR-1295).

            // HMAC using SHA-2 Functions
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.2

            // HS256
            yield return new KeyedHashSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.HmacSha256,
                HashAlgorithmName.SHA256,
                HMACSHA256.TryHashData
            );

            // HS384
            yield return new KeyedHashSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.HmacSha384,
                HashAlgorithmName.SHA384,
                HMACSHA384.TryHashData
            );

            // HS512
            yield return new KeyedHashSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.HmacSha512,
                HashAlgorithmName.SHA512,
                HMACSHA512.TryHashData
            );

            // Digital Signature with RSASSA-PKCS1-v1_5
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.3

            // RS256
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha256,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            );

            // RS384
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha384,
                HashAlgorithmName.SHA384,
                RSASignaturePadding.Pkcs1
            );

            // RS512
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha512,
                HashAlgorithmName.SHA512,
                RSASignaturePadding.Pkcs1
            );

            // Digital Signature with RSASSA-PSS
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.5

            // PS256
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss
            );

            // PS384
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha384,
                HashAlgorithmName.SHA384,
                RSASignaturePadding.Pss
            );

            // PS512
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha512,
                HashAlgorithmName.SHA512,
                RSASignaturePadding.Pss
            );

            // Digital Signature with ECDSA
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.4
            // ES256, ES384 and ES512, and each ECDSA algorithm a package registers, such as ES256K (RFC 8812).

            foreach (var specification in EccSignatureAlgorithms)
            {
                yield return new EccSignatureAlgorithm(specification);
            }

            // Module-Lattice-Based Digital Signature Algorithm (ML-DSA)
            // https://datatracker.ietf.org/doc/rfc9964/ (FIPS 204). Advertised only where the running OS provides
            // ML-DSA, so discovery / JWKS never offer an algorithm the host cannot fulfill.
            if (MLDsa.IsSupported)
            {
                // ML-DSA-44
                yield return new MlDsaSignatureAlgorithm(
                    AlgorithmCodes.DigitalSignature.MlDsa44,
                    MLDsaAlgorithm.MLDsa44,
                    HashAlgorithmName.SHA256
                );

                // ML-DSA-65
                yield return new MlDsaSignatureAlgorithm(
                    AlgorithmCodes.DigitalSignature.MlDsa65,
                    MLDsaAlgorithm.MLDsa65,
                    HashAlgorithmName.SHA384
                );

                // ML-DSA-87
                yield return new MlDsaSignatureAlgorithm(
                    AlgorithmCodes.DigitalSignature.MlDsa87,
                    MLDsaAlgorithm.MLDsa87,
                    HashAlgorithmName.SHA512
                );
            }

            #endregion

            #region Key Management Algorithms

            // Direct Encryption with a Shared Symmetric Key
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.5

            yield return DirectKeyManagementAlgorithm.Singleton;

            // AES Key Wrap
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.4

            // A128KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes128,
                kekSizeBits: 128
            );

            // A192KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes192,
                kekSizeBits: 192
            );

            // A256KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes256,
                kekSizeBits: 256
            );

            // AES GCM
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.7

            // A128GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes128Gcm,
                kekSizeBits: 128
            );

            // A192GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes192Gcm,
                kekSizeBits: 192
            );

            // A256GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes256Gcm,
                kekSizeBits: 256
            );

            // RSAES-PKCS1-v1_5 (RSA1_5, RFC 7518 §4.2) is deliberately not registered: RFC 8725 §3.2 says to avoid it
            // because of padding-oracle attacks (ADR-1282).

            // RSA-OAEP
            // RSA-OAEP-256
            // RSA-OAEP-384
            // RSA-OAEP-512
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.3

            yield return new RsaOaepKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep,
                HashAlgorithmName.SHA1
            );

            yield return new RsaOaepKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep256,
                HashAlgorithmName.SHA256
            );

            yield return new RsaOaepKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep384,
                HashAlgorithmName.SHA384
            );

            yield return new RsaOaepKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep512,
                HashAlgorithmName.SHA512
            );

            // Key Agreement with Elliptic Curve Diffie-Hellman Ephemeral Static (ECDH-ES)
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.6
            // Each name serves every key type that has a registered IEcdhKeyAgreement: EC keys here, and the OKP keys of
            // RFC 8037 §3.2 from the package that contributes them (ADR-1389).

            // ECDH-ES
            yield return new EcdhKeyManagementAlgorithm(SecretKeyTypeCatalog, EcdhKeyAgreements);

            // ECDH-ES+A128KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                SecretKeyTypeCatalog,
                EcdhKeyAgreements,
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes128,
                kekSizeBits: 128
            );

            // ECDH-ES+A192KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                SecretKeyTypeCatalog,
                EcdhKeyAgreements,
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes192,
                kekSizeBits: 192
            );

            // ECDH-ES+A256KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                SecretKeyTypeCatalog,
                EcdhKeyAgreements,
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes256,
                kekSizeBits: 256
            );

            // PBES2
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.8

            // PBES2-HS256+A128KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha256Aes128,
                HashAlgorithmName.SHA256,
                keySizeBits: 128,
                maxIterationCount: 310000
            );

            // PBES2-HS384+A192KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha384Aes192,
                HashAlgorithmName.SHA384,
                keySizeBits: 192,
                maxIterationCount: 250000
            );

            // PBES2-HS512+A256KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha512Aes256,
                HashAlgorithmName.SHA512,
                keySizeBits: 256,
                maxIterationCount: 120000
            );

            #endregion

            #region Encryption Algorithms

            // AES_CBC_HMAC_SHA2 Algorithms
            // https://datatracker.ietf.org/doc/html/rfc7518#section-5.2

            // A128CBC-HS256
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,
                HMACSHA256.TryHashData,
                cekSizeBits: 256
            );

            // A192CBC-HS384
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192CbcHmacSha384,
                HMACSHA384.TryHashData,
                cekSizeBits: 384
            );

            // A256CBC-HS512
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256CbcHmacSha512,
                HMACSHA512.TryHashData,
                cekSizeBits: 512
            );

            // Content Encryption with AES GCM
            // https://datatracker.ietf.org/doc/html/rfc7518#section-5.3

            // A128GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
                cekSizeBits: 128
            );

            // A192GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192Gcm,
                cekSizeBits: 192
            );

            // A256GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
                cekSizeBits: 256
            );

            #endregion

            #region Compression Algorithms

            // https://datatracker.ietf.org/doc/html/rfc7516#section-4.1.3

            // none
            yield return NoneCompressionAlgorithm.Singleton;

            // DEF
            yield return DeflateCompressionAlgorithm.Singleton;

            #endregion
        }
    }

    private static ImmutableArray<EccSignatureAlgorithmSpecification> ComposeEccSignatureAlgorithms(
        IEnumerable<EccSignatureAlgorithmSpecification> registered
    )
    {
        var composed = new List<EccSignatureAlgorithmSpecification>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var specification in BuiltInEccSignatureAlgorithms.Concat(registered))
        {
            if (string.IsNullOrEmpty(specification.Code))
            {
                throw new InvalidOperationException(
                    "A registered ECDSA signature algorithm has an empty code."
                );
            }

            if (!codes.Add(specification.Code))
            {
                throw new InvalidOperationException(
                    $"The ECDSA signature algorithm '{specification.Code}' is registered more than once."
                );
            }

            composed.Add(specification);
        }

        return [.. composed];
    }
}
