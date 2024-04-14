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
using Microsoft.Extensions.Primitives;
using NCode.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Jose.Algorithms.Compression;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.Collections;
using NCode.Jose.Infrastructure;

namespace NCode.Jose.Algorithms;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="Algorithm"/> instances supported by this library.
/// </summary>
public sealed class DefaultAlgorithmDataSource : ICollectionDataSource<Algorithm>
{
    private IAesKeyWrap AesKeyWrap { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAlgorithmDataSource"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the common implementation for <c>AES Key Wrap</c> functionality.</param>
    public DefaultAlgorithmDataSource(IAesKeyWrap aesKeyWrap)
    {
        AesKeyWrap = aesKeyWrap;
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;

    /// <inheritdoc />
    public IEnumerable<Algorithm> Collection
    {
        get
        {
            #region Signature Algorithms

            // None
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.6
            yield return NoneSignatureAlgorithm.Singleton;

            // HMAC using SHA-2 Functions
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.2

            // HS256
            yield return new KeyedHashSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.HmacSha256,
                HashAlgorithmName.SHA256,
                HMACSHA256.TryHashData);

            // HS384
            yield return new KeyedHashSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.HmacSha384,
                HashAlgorithmName.SHA384,
                HMACSHA384.TryHashData);

            // HS512
            yield return new KeyedHashSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.HmacSha512,
                HashAlgorithmName.SHA512,
                HMACSHA512.TryHashData);

            // Digital Signature with RSASSA-PKCS1-v1_5
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.3

            // RS256
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha256,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // RS384
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha384,
                HashAlgorithmName.SHA384,
                RSASignaturePadding.Pkcs1);

            // RS512
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha512,
                HashAlgorithmName.SHA512,
                RSASignaturePadding.Pkcs1);

            // Digital Signature with RSASSA-PSS
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.5

            // PS256
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);

            // PS384
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha384,
                HashAlgorithmName.SHA384,
                RSASignaturePadding.Pss);

            // PS512
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha512,
                HashAlgorithmName.SHA512,
                RSASignaturePadding.Pss);

            // Digital Signature with ECDSA
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.4

            // ES256
            yield return new EccSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.EcdsaSha256,
                HashAlgorithmName.SHA256);

            // ES384
            yield return new EccSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.EcdsaSha384,
                HashAlgorithmName.SHA384);

            // ES512
            yield return new EccSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.EcdsaSha512,
                HashAlgorithmName.SHA512);

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
                kekSizeBits: 128);

            // A192KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes192,
                kekSizeBits: 192);

            // A256KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes256,
                kekSizeBits: 256);

            // AES GCM
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.7

            // A128GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes128Gcm,
                kekSizeBits: 128);

            // A192GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes192Gcm,
                kekSizeBits: 192);

            // A256GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes256Gcm,
                kekSizeBits: 256);

            // RSA1_5
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.2

            yield return new RsaKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaPkcs1,
                RSAEncryptionPadding.Pkcs1);

            // RSA-OAEP
            // RSA-OAEP-256
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.3

            yield return new RsaKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep,
                RSAEncryptionPadding.OaepSHA1);

            yield return new RsaKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep256,
                RSAEncryptionPadding.OaepSHA256);

            // Key Agreement with Elliptic Curve Diffie-Hellman Ephemeral Static (ECDH-ES)
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.6

            // ECDH-ES
            yield return EcdhKeyManagementAlgorithm.Singleton;

            // ECDH-ES+A128KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes128,
                kekSizeBits: 128);

            // ECDH-ES+A192KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes192,
                kekSizeBits: 192);

            // ECDH-ES+A256KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes256,
                kekSizeBits: 256);

            // PBES2
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.8

            // PBES2-HS256+A128KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha256Aes128,
                HashAlgorithmName.SHA256,
                keySizeBits: 128,
                maxIterationCount: 310000);

            // PBES2-HS384+A192KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha384Aes192,
                HashAlgorithmName.SHA384,
                keySizeBits: 192,
                maxIterationCount: 250000);

            // PBES2-HS512+A256KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha512Aes256,
                HashAlgorithmName.SHA512,
                keySizeBits: 256,
                maxIterationCount: 120000);

            #endregion

            #region Encryption Algorithms

            // AES_CBC_HMAC_SHA2 Algorithms
            // https://datatracker.ietf.org/doc/html/rfc7518#section-5.2

            // A128CBC-HS256
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,
                HMACSHA256.TryHashData,
                cekSizeBits: 256);

            // A192CBC-HS384
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192CbcHmacSha384,
                HMACSHA384.TryHashData,
                cekSizeBits: 384);

            // A256CBC-HS512
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256CbcHmacSha512,
                HMACSHA512.TryHashData,
                cekSizeBits: 512);

            // Content Encryption with AES GCM
            // https://datatracker.ietf.org/doc/html/rfc7518#section-5.3

            // A128GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
                cekSizeBits: 128);

            // A192GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192Gcm,
                cekSizeBits: 192);

            // A256GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
                cekSizeBits: 256);

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
}
