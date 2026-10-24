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

using System.Diagnostics.CodeAnalysis;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.ClientCertificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// A parsed X.509 certificate revocation list (RFC 5280 §5.1) of the version 2 form, together with the bytes its
/// signature covers. Parsing reads the list; it does not verify who signed it.
/// </summary>
internal sealed class CertificateRevocationList
{
    private const string DeltaCrlIndicatorOid = "2.5.29.27";
    private const string IssuingDistributionPointOid = "2.5.29.28";
    private const string Mgf1Oid = "1.2.840.113549.1.1.8";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";

    private static readonly Asn1Tag ExtensionsTag = new(TagClass.ContextSpecific, 0, true);

    private static readonly Asn1Tag PssHashAlgorithmTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag PssMaskGenAlgorithmTag = new(TagClass.ContextSpecific, 1, true);
    private static readonly Asn1Tag PssSaltLengthTag = new(TagClass.ContextSpecific, 2, true);
    private static readonly Asn1Tag PssTrailerFieldTag = new(TagClass.ContextSpecific, 3, true);

    private static readonly Asn1Tag DistributionPointNameTag = new(
        TagClass.ContextSpecific,
        0,
        true
    );

    private static readonly Asn1Tag OnlyContainsUserCertificatesTag = new(
        TagClass.ContextSpecific,
        1
    );

    private static readonly Asn1Tag OnlyContainsAuthorityCertificatesTag = new(
        TagClass.ContextSpecific,
        2
    );

    private static readonly Asn1Tag OnlySomeReasonsTag = new(TagClass.ContextSpecific, 3);

    private static readonly Asn1Tag IndirectCrlTag = new(TagClass.ContextSpecific, 4);

    private static readonly Asn1Tag OnlyContainsAttributeCertificatesTag = new(
        TagClass.ContextSpecific,
        5
    );

    /// <summary>
    /// Gets the name of the authority that issued the list.
    /// </summary>
    public required X500DistinguishedName Issuer { get; init; }

    /// <summary>
    /// Gets the time the list was issued.
    /// </summary>
    public required DateTimeOffset ThisUpdate { get; init; }

    /// <summary>
    /// Gets the time the next list is due, or <see langword="null"/> when the list does not say.
    /// </summary>
    public required DateTimeOffset? NextUpdate { get; init; }

    /// <summary>
    /// Gets the serial numbers of the certificates the list revokes.
    /// </summary>
    public required IReadOnlySet<BigInteger> RevokedSerialNumbers { get; init; }

    /// <summary>
    /// Gets a value indicating whether the list uses a feature that this reader does not support, so that the list
    /// must not be relied on to say a certificate is not revoked.
    /// </summary>
    public required bool HasUnsupportedFeatures { get; init; }

    /// <summary>
    /// Gets the locations the list says it is published at. The list covers every location when it names none.
    /// </summary>
    public required IReadOnlyList<string> DistributionPointUris { get; init; }

    /// <summary>
    /// Gets a value indicating whether the list covers only end-entity certificates.
    /// </summary>
    public required bool OnlyContainsUserCertificates { get; init; }

    /// <summary>
    /// Gets a value indicating whether the list covers only certificate authority certificates.
    /// </summary>
    public required bool OnlyContainsAuthorityCertificates { get; init; }

    /// <summary>
    /// Gets the object identifier of the algorithm that signed the list.
    /// </summary>
    public required string SignatureAlgorithm { get; init; }

    /// <summary>
    /// Gets the encoded parameters of the algorithm that signed the list, or an empty value when the algorithm
    /// carries none.
    /// </summary>
    public required ReadOnlyMemory<byte> SignatureAlgorithmParameters { get; init; }

    /// <summary>
    /// Gets the encoded bytes the signature covers.
    /// </summary>
    public required ReadOnlyMemory<byte> SignedData { get; init; }

    /// <summary>
    /// Gets the signature over the signed bytes.
    /// </summary>
    public required ReadOnlyMemory<byte> Signature { get; init; }

    /// <summary>
    /// Reads a DER-encoded certificate revocation list. Returns <see langword="false"/> when the bytes are not a
    /// well-formed list.
    /// </summary>
    public static bool TryParse(
        ReadOnlyMemory<byte> encoded,
        [NotNullWhen(true)] out CertificateRevocationList? crl
    )
    {
        crl = null;

        try
        {
            var reader = new AsnReader(encoded, AsnEncodingRules.DER);
            var certificateList = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            var signedData = certificateList.ReadEncodedValue();
            var outerAlgorithm = certificateList.ReadEncodedValue();
            var signature = certificateList.ReadBitString(out var unusedBitCount);
            certificateList.ThrowIfNotEmpty();
            if (unusedBitCount != 0)
                return false;

            var tbs = new AsnReader(signedData, AsnEncodingRules.DER).ReadSequence();

            if (tbs.PeekTag() == Asn1Tag.Integer && tbs.ReadInteger() != BigInteger.One)
                return false;

            var innerAlgorithm = tbs.ReadEncodedValue();
            if (!innerAlgorithm.Span.SequenceEqual(outerAlgorithm.Span))
                return false;

            var algorithm = new AsnReader(innerAlgorithm, AsnEncodingRules.DER).ReadSequence();
            var signatureAlgorithm = algorithm.ReadObjectIdentifier();
            var signatureParameters = algorithm.HasData
                ? algorithm.ReadEncodedValue()
                : ReadOnlyMemory<byte>.Empty;
            algorithm.ThrowIfNotEmpty();

            var issuer = new X500DistinguishedName(tbs.ReadEncodedValue().Span);
            var thisUpdate = ReadTime(tbs);

            DateTimeOffset? nextUpdate = null;
            if (tbs.HasData && IsTime(tbs.PeekTag()))
                nextUpdate = ReadTime(tbs);

            var hasUnsupportedFeatures = false;
            var distributionPointUris = new List<string>();
            var onlyContainsUserCertificates = false;
            var onlyContainsAuthorityCertificates = false;

            var revokedSerialNumbers = new HashSet<BigInteger>();
            if (tbs.HasData && tbs.PeekTag() == Asn1Tag.Sequence)
            {
                var revokedCertificates = tbs.ReadSequence();
                while (revokedCertificates.HasData)
                {
                    var entry = revokedCertificates.ReadSequence();
                    revokedSerialNumbers.Add(entry.ReadInteger());
                    ReadTime(entry);

                    if (!entry.HasData)
                        continue;

                    var entryExtensions = entry.ReadSequence();
                    entry.ThrowIfNotEmpty();
                    while (entryExtensions.HasData)
                    {
                        ReadExtension(entryExtensions, out _, out var critical, out _);
                        if (critical)
                            hasUnsupportedFeatures = true;
                    }
                }
            }

            if (tbs.HasData && tbs.PeekTag() == ExtensionsTag)
            {
                var explicitExtensions = tbs.ReadSequence(ExtensionsTag);
                var extensions = explicitExtensions.ReadSequence();
                explicitExtensions.ThrowIfNotEmpty();
                while (extensions.HasData)
                {
                    ReadExtension(extensions, out var oid, out var critical, out var value);
                    if (oid == IssuingDistributionPointOid)
                    {
                        if (
                            !TryReadIssuingDistributionPoint(
                                value,
                                distributionPointUris,
                                out onlyContainsUserCertificates,
                                out onlyContainsAuthorityCertificates
                            )
                        )
                        {
                            hasUnsupportedFeatures = true;
                        }
                    }
                    else if (oid == DeltaCrlIndicatorOid || critical)
                    {
                        hasUnsupportedFeatures = true;
                    }
                }
            }

            tbs.ThrowIfNotEmpty();

            crl = new CertificateRevocationList
            {
                Issuer = issuer,
                ThisUpdate = thisUpdate,
                NextUpdate = nextUpdate,
                RevokedSerialNumbers = revokedSerialNumbers,
                HasUnsupportedFeatures = hasUnsupportedFeatures,
                DistributionPointUris = distributionPointUris,
                OnlyContainsUserCertificates = onlyContainsUserCertificates,
                OnlyContainsAuthorityCertificates = onlyContainsAuthorityCertificates,
                SignatureAlgorithm = signatureAlgorithm,
                SignatureAlgorithmParameters = signatureParameters,
                SignedData = signedData,
                Signature = signature,
            };
            return true;
        }
        catch (AsnContentException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the list revokes the specified certificate, by comparing the certificate's serial number
    /// with the serial numbers the list revokes. The caller decides whether the list was issued by the certificate's
    /// issuer.
    /// </summary>
    public bool IsRevoked(X509Certificate2 certificate)
    {
        var serialNumber = new BigInteger(
            certificate.SerialNumberBytes.Span,
            isUnsigned: false,
            isBigEndian: true
        );
        return RevokedSerialNumbers.Contains(serialNumber);
    }

    /// <summary>
    /// Determines whether the list is within the scope it declares for a certificate whose CRL distribution point is
    /// the specified location. The list covers the location when it names no locations or names this one, and it
    /// covers the kind of certificate unless it declares that it holds only the other kind.
    /// </summary>
    public bool Covers(string distributionPointUri, bool isAuthority)
    {
        if (
            DistributionPointUris.Count > 0
            && !DistributionPointUris.Contains(distributionPointUri, StringComparer.Ordinal)
        )
        {
            return false;
        }

        if (OnlyContainsUserCertificates && isAuthority)
            return false;

        if (OnlyContainsAuthorityCertificates && !isAuthority)
            return false;

        return true;
    }

    /// <summary>
    /// Determines whether the specified certificate issued and signed the list. The certificate must be allowed to
    /// sign revocation lists, its subject must match the issuer named in the list, and the signature must verify
    /// with the certificate's public key.
    /// </summary>
    public bool IsSignedBy(X509Certificate2 issuer)
    {
        if (
            issuer.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault() is { } keyUsage
            && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.CrlSign)
        )
        {
            return false;
        }

        try
        {
            var issuerName = DistinguishedName.FromX500(Issuer);
            if (!issuerName.Matches(DistinguishedName.FromX500(issuer.SubjectName)))
                return false;

            // RSA PKCS#1 v1.5, RSASSA-PSS (MGF1 over the same hash, salt as long as the hash) and ECDSA signatures,
            // each with SHA-256, SHA-384 or SHA-512, are supported. A list signed with SHA-1, or by a signer other
            // than the issuing certificate (an indirect or delegated signer), is not accepted.
            return SignatureAlgorithm switch
            {
                "1.2.840.113549.1.1.11" => VerifyRsa(
                    issuer,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                ),
                "1.2.840.113549.1.1.12" => VerifyRsa(
                    issuer,
                    HashAlgorithmName.SHA384,
                    RSASignaturePadding.Pkcs1
                ),
                "1.2.840.113549.1.1.13" => VerifyRsa(
                    issuer,
                    HashAlgorithmName.SHA512,
                    RSASignaturePadding.Pkcs1
                ),
                "1.2.840.113549.1.1.10" => TryGetPssHashAlgorithm(
                    SignatureAlgorithmParameters,
                    out var pssHash
                ) && VerifyRsa(issuer, pssHash, RSASignaturePadding.Pss),
                "1.2.840.10045.4.3.2" => VerifyEcdsa(issuer, HashAlgorithmName.SHA256),
                "1.2.840.10045.4.3.3" => VerifyEcdsa(issuer, HashAlgorithmName.SHA384),
                "1.2.840.10045.4.3.4" => VerifyEcdsa(issuer, HashAlgorithmName.SHA512),
                _ => false,
            };
        }
        catch (CryptographicException)
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

    private bool VerifyRsa(
        X509Certificate2 issuer,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding padding
    )
    {
        using var key = issuer.GetRSAPublicKey();
        return key is not null
            && key.VerifyData(SignedData.Span, Signature.Span, hashAlgorithm, padding);
    }

    private bool VerifyEcdsa(X509Certificate2 issuer, HashAlgorithmName hashAlgorithm)
    {
        using var key = issuer.GetECDsaPublicKey();
        return key is not null
            && key.VerifyData(
                SignedData.Span,
                Signature.Span,
                hashAlgorithm,
                DSASignatureFormat.Rfc3279DerSequence
            );
    }

    private static bool TryReadIssuingDistributionPoint(
        byte[] value,
        List<string> uris,
        out bool onlyContainsUserCertificates,
        out bool onlyContainsAuthorityCertificates
    )
    {
        onlyContainsUserCertificates = false;
        onlyContainsAuthorityCertificates = false;

        var reader = new AsnReader(value, AsnEncodingRules.DER);
        var point = reader.ReadSequence();
        reader.ThrowIfNotEmpty();

        while (point.HasData)
        {
            var tag = point.PeekTag();
            if (tag == DistributionPointNameTag)
            {
                var distributionPointName = point.ReadSequence(DistributionPointNameTag);
                if (!CrlDistributionPoints.TryReadFullNameUris(distributionPointName, uris))
                    return false;
            }
            else if (tag == OnlyContainsUserCertificatesTag)
            {
                onlyContainsUserCertificates = point.ReadBoolean(OnlyContainsUserCertificatesTag);
            }
            else if (tag == OnlyContainsAuthorityCertificatesTag)
            {
                onlyContainsAuthorityCertificates = point.ReadBoolean(
                    OnlyContainsAuthorityCertificatesTag
                );
            }
            else if (tag == OnlySomeReasonsTag)
            {
                point.ReadEncodedValue();
                return false;
            }
            else if (tag == IndirectCrlTag)
            {
                if (point.ReadBoolean(IndirectCrlTag))
                    return false;
            }
            else if (tag == OnlyContainsAttributeCertificatesTag)
            {
                if (point.ReadBoolean(OnlyContainsAttributeCertificatesTag))
                    return false;
            }
            else
            {
                point.ReadEncodedValue();
                return false;
            }
        }

        return true;
    }

    private static bool IsTime(Asn1Tag tag) =>
        tag == Asn1Tag.UtcTime || tag == Asn1Tag.GeneralizedTime;

    private static DateTimeOffset ReadTime(AsnReader reader) =>
        reader.PeekTag() == Asn1Tag.GeneralizedTime
            ? reader.ReadGeneralizedTime()
            : reader.ReadUtcTime();

    private static void ReadExtension(
        AsnReader extensions,
        out string oid,
        out bool critical,
        out byte[] value
    )
    {
        var extension = extensions.ReadSequence();
        oid = extension.ReadObjectIdentifier();
        critical = extension.PeekTag() == Asn1Tag.Boolean && extension.ReadBoolean();
        value = extension.ReadOctetString();
        extension.ThrowIfNotEmpty();
    }
}
