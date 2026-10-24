#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Verifies the signatures that revocation information and certificates carry. RSA PKCS#1 v1.5, RSASSA-PSS (MGF1 over
/// the same hash, salt as long as the hash) and ECDSA signatures, each with SHA-256, SHA-384 or SHA-512, are
/// supported; a signature made with SHA-1 or with any other algorithm is not accepted.
/// </summary>
internal static class RevocationSignature
{
    private const string Mgf1Oid = "1.2.840.113549.1.1.8";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";

    private static readonly Asn1Tag PssHashAlgorithmTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag PssMaskGenAlgorithmTag = new(TagClass.ContextSpecific, 1, true);
    private static readonly Asn1Tag PssSaltLengthTag = new(TagClass.ContextSpecific, 2, true);
    private static readonly Asn1Tag PssTrailerFieldTag = new(TagClass.ContextSpecific, 3, true);

    /// <summary>
    /// Determines whether a signature over the signed data verifies with the public key of the signer. Returns
    /// <see langword="false"/> when the signature algorithm is not supported, the signer's key does not match the
    /// algorithm, or the signature does not verify.
    /// </summary>
    public static bool Verify(
        X509Certificate2 signer,
        string signatureAlgorithm,
        ReadOnlyMemory<byte> parameters,
        ReadOnlySpan<byte> signedData,
        ReadOnlySpan<byte> signature
    )
    {
        try
        {
            return signatureAlgorithm switch
            {
                "1.2.840.113549.1.1.11" => VerifyRsa(
                    signer,
                    signedData,
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                ),
                "1.2.840.113549.1.1.12" => VerifyRsa(
                    signer,
                    signedData,
                    signature,
                    HashAlgorithmName.SHA384,
                    RSASignaturePadding.Pkcs1
                ),
                "1.2.840.113549.1.1.13" => VerifyRsa(
                    signer,
                    signedData,
                    signature,
                    HashAlgorithmName.SHA512,
                    RSASignaturePadding.Pkcs1
                ),
                "1.2.840.113549.1.1.10" => TryGetPssHashAlgorithm(parameters, out var pssHash)
                    && VerifyRsa(signer, signedData, signature, pssHash, RSASignaturePadding.Pss),
                "1.2.840.10045.4.3.2" => VerifyEcdsa(
                    signer,
                    signedData,
                    signature,
                    HashAlgorithmName.SHA256
                ),
                "1.2.840.10045.4.3.3" => VerifyEcdsa(
                    signer,
                    signedData,
                    signature,
                    HashAlgorithmName.SHA384
                ),
                "1.2.840.10045.4.3.4" => VerifyEcdsa(
                    signer,
                    signedData,
                    signature,
                    HashAlgorithmName.SHA512
                ),
                _ => false,
            };
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the specified issuer signed the specified certificate, by verifying the certificate's own
    /// signature with the issuer's public key. The names and extensions of the two certificates are not compared.
    /// </summary>
    public static bool VerifyCertificateSignature(
        X509Certificate2 certificate,
        X509Certificate2 issuer
    )
    {
        // Certificate ::= SEQUENCE { tbsCertificate, signatureAlgorithm AlgorithmIdentifier, signatureValue BIT STRING }
        try
        {
            var reader = new AsnReader(certificate.RawData, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            var tbs = sequence.ReadEncodedValue();
            var algorithm = sequence.ReadSequence();
            var oid = algorithm.ReadObjectIdentifier();
            var parameters = algorithm.HasData
                ? algorithm.ReadEncodedValue()
                : ReadOnlyMemory<byte>.Empty;
            algorithm.ThrowIfNotEmpty();

            var signature = sequence.ReadBitString(out var unusedBitCount);
            sequence.ThrowIfNotEmpty();

            return unusedBitCount == 0 && Verify(issuer, oid, parameters, tbs.Span, signature);
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the parameters of an RSASSA-PSS signature algorithm and returns the hash algorithm they name. The
    /// parameters are accepted only when they name SHA-256, SHA-384 or SHA-512, a mask generation function MGF1 over
    /// the same hash, a salt as long as the hash, and no trailer field or the trailer field <c>1</c>. Parameters
    /// that leave a field to its default (SHA-1), or that are not well formed, are refused.
    /// </summary>
    internal static bool TryGetPssHashAlgorithm(
        ReadOnlyMemory<byte> parameters,
        out HashAlgorithmName hashAlgorithm
    )
    {
        hashAlgorithm = default;

        try
        {
            var reader = new AsnReader(parameters, AsnEncodingRules.DER);
            var pssParameters = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            var hashField = pssParameters.ReadSequence(PssHashAlgorithmTag);
            var hashOid = ReadHashAlgorithmIdentifier(hashField);
            hashField.ThrowIfNotEmpty();

            var (hashName, hashLength) = hashOid switch
            {
                Sha256Oid => (HashAlgorithmName.SHA256, 32),
                Sha384Oid => (HashAlgorithmName.SHA384, 48),
                Sha512Oid => (HashAlgorithmName.SHA512, 64),
                _ => (default(HashAlgorithmName), 0),
            };
            if (hashLength == 0)
                return false;

            var maskGenField = pssParameters.ReadSequence(PssMaskGenAlgorithmTag);
            var maskGen = maskGenField.ReadSequence();
            maskGenField.ThrowIfNotEmpty();
            if (maskGen.ReadObjectIdentifier() != Mgf1Oid)
                return false;

            var maskGenHashOid = ReadHashAlgorithmIdentifier(maskGen);
            maskGen.ThrowIfNotEmpty();
            if (maskGenHashOid != hashOid)
                return false;

            var saltLengthField = pssParameters.ReadSequence(PssSaltLengthTag);
            var saltLength = saltLengthField.ReadInteger();
            saltLengthField.ThrowIfNotEmpty();
            if (saltLength != hashLength)
                return false;

            if (pssParameters.HasData && pssParameters.PeekTag() == PssTrailerFieldTag)
            {
                var trailerField = pssParameters.ReadSequence(PssTrailerFieldTag);
                var trailer = trailerField.ReadInteger();
                trailerField.ThrowIfNotEmpty();
                if (trailer != BigInteger.One)
                    return false;
            }

            pssParameters.ThrowIfNotEmpty();

            hashAlgorithm = hashName;
            return true;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    private static string ReadHashAlgorithmIdentifier(AsnReader reader)
    {
        var identifier = reader.ReadSequence();
        var oid = identifier.ReadObjectIdentifier();
        if (identifier.HasData)
            identifier.ReadNull();

        identifier.ThrowIfNotEmpty();
        return oid;
    }

    private static bool VerifyRsa(
        X509Certificate2 signer,
        ReadOnlySpan<byte> signedData,
        ReadOnlySpan<byte> signature,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding padding
    )
    {
        using var key = signer.GetRSAPublicKey();
        return key is not null && key.VerifyData(signedData, signature, hashAlgorithm, padding);
    }

    private static bool VerifyEcdsa(
        X509Certificate2 signer,
        ReadOnlySpan<byte> signedData,
        ReadOnlySpan<byte> signature,
        HashAlgorithmName hashAlgorithm
    )
    {
        using var key = signer.GetECDsaPublicKey();
        return key is not null
            && key.VerifyData(
                signedData,
                signature,
                hashAlgorithm,
                DSASignatureFormat.Rfc3279DerSequence
            );
    }
}
