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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.ClientCertificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// A parsed X.509 certificate revocation list (RFC 5280 §5.1) of the version 2 form: its issuer, validity, entries by
/// serial number, issuing distribution point, CRL number, delta indicator, authority key identifier and freshest CRL
/// locations, together with the bytes its signature covers. Parsing reads the list; it does not verify who signed it.
/// <see cref="IsSignedBy"/> decides whether a signer signed the list, and computes the verdict for each signer once
/// per list.
/// </summary>
internal sealed class CertificateRevocationList
{
    private const string DeltaCrlIndicatorOid = "2.5.29.27";
    private const string IssuingDistributionPointOid = "2.5.29.28";
    private const string CrlNumberOid = "2.5.29.20";
    private const string AuthorityKeyIdentifierOid = "2.5.29.35";
    private const string FreshestCrlOid = "2.5.29.46";
    private const string IssuerAltNameOid = "2.5.29.18";
    private const string AuthorityInfoAccessOid = "1.3.6.1.5.5.7.1.1";
    private const string ReasonCodeOid = "2.5.29.21";
    private const string HoldInstructionCodeOid = "2.5.29.23";
    private const string InvalidityDateOid = "2.5.29.24";
    private const string CertificateIssuerOid = "2.5.29.29";

    private static readonly Asn1Tag ExtensionsTag = new(TagClass.ContextSpecific, 0, true);

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

    private ConcurrentDictionary<string, bool> SignerVerdicts { get; } =
        new(StringComparer.Ordinal);

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
    /// Gets the entries of the list by serial number. A serial number has more than one entry when the list is
    /// indirect and lists certificates of several issuers that share it.
    /// </summary>
    public required IReadOnlyDictionary<
        BigInteger,
        IReadOnlyList<CrlEntry>
    > EntriesBySerialNumber { get; init; }

    /// <summary>
    /// Gets a value indicating whether the list uses a feature that this reader does not support, so that the list
    /// must not be relied on to say a certificate is not revoked.
    /// </summary>
    public required bool HasUnsupportedFeatures { get; init; }

    /// <summary>
    /// Gets the issuing distribution point of the list, which declares the scope of the list, or
    /// <see langword="null"/> when the list declares none and so covers every certificate of its issuer.
    /// </summary>
    public required CrlIssuingDistributionPoint? IssuingDistributionPoint { get; init; }

    /// <summary>
    /// Gets the sequence number of the list, or <see langword="null"/> when the list carries none.
    /// </summary>
    public required BigInteger? CrlNumber { get; init; }

    /// <summary>
    /// Gets the number of the complete list that a delta list is based on, or <see langword="null"/> when the list is
    /// not a delta list.
    /// </summary>
    public required BigInteger? DeltaBaseCrlNumber { get; init; }

    /// <summary>
    /// Gets a value indicating whether the list is a delta list, which holds only the changes since a complete list.
    /// </summary>
    public bool IsDelta => DeltaBaseCrlNumber is not null;

    /// <summary>
    /// Gets the authority key identifier extension value of the list, or an empty value when the list carries none.
    /// </summary>
    public required ReadOnlyMemory<byte> AuthorityKeyIdentifier { get; init; }

    /// <summary>
    /// Gets the <c>http</c> and <c>https</c> locations of the delta lists that the freshest CRL extension of the list
    /// names.
    /// </summary>
    public required IReadOnlyList<string> FreshestCrlUris { get; init; }

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
            CrlIssuingDistributionPoint? issuingDistributionPoint = null;
            BigInteger? crlNumber = null;
            BigInteger? deltaBaseCrlNumber = null;
            var authorityKeyIdentifier = ReadOnlyMemory<byte>.Empty;
            var freshestCrlUris = new List<string>();

            var entries = new Dictionary<BigInteger, List<CrlEntry>>();
            var currentIssuer = issuer;
            var hasCertificateIssuer = false;
            if (tbs.HasData && tbs.PeekTag() == Asn1Tag.Sequence)
            {
                var revokedCertificates = tbs.ReadSequence();
                while (revokedCertificates.HasData)
                {
                    var entry = revokedCertificates.ReadSequence();
                    var serialNumber = entry.ReadInteger();
                    ReadTime(entry);

                    var isRemoveFromCrl = false;
                    if (entry.HasData)
                    {
                        var entryExtensions = entry.ReadSequence();
                        entry.ThrowIfNotEmpty();
                        while (entryExtensions.HasData)
                        {
                            ReadExtension(
                                entryExtensions,
                                out var entryOid,
                                out var entryCritical,
                                out var entryValue
                            );
                            if (entryOid == CertificateIssuerOid)
                            {
                                hasCertificateIssuer = true;
                                var namesReader = new AsnReader(entryValue, AsnEncodingRules.DER);
                                var generalNames = namesReader.ReadSequence();
                                namesReader.ThrowIfNotEmpty();
                                if (
                                    CrlDistributionPoints.TryReadDirectoryName(
                                        generalNames,
                                        out var certificateIssuer
                                    )
                                )
                                {
                                    currentIssuer = certificateIssuer;
                                }
                                else
                                {
                                    hasUnsupportedFeatures = true;
                                }
                            }
                            else if (entryOid == ReasonCodeOid)
                            {
                                var reasonReader = new AsnReader(entryValue, AsnEncodingRules.DER);
                                var reason = reasonReader.ReadEnumeratedBytes();
                                reasonReader.ThrowIfNotEmpty();
                                isRemoveFromCrl = reason.Span is [8];
                            }
                            else if (
                                entryOid != HoldInstructionCodeOid
                                && entryOid != InvalidityDateOid
                                && entryCritical
                            )
                            {
                                hasUnsupportedFeatures = true;
                            }
                        }
                    }

                    if (!entries.TryGetValue(serialNumber, out var serialEntries))
                    {
                        serialEntries = [];
                        entries.Add(serialNumber, serialEntries);
                    }

                    serialEntries.Add(new CrlEntry(serialNumber, currentIssuer, isRemoveFromCrl));
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
                    switch (oid)
                    {
                        case IssuingDistributionPointOid:
                            if (TryReadIssuingDistributionPoint(value, out var point))
                                issuingDistributionPoint = point;
                            else
                                hasUnsupportedFeatures = true;
                            break;

                        case CrlNumberOid:
                            crlNumber = ReadInteger(value);
                            break;

                        case DeltaCrlIndicatorOid:
                            deltaBaseCrlNumber = ReadInteger(value);
                            break;

                        case AuthorityKeyIdentifierOid:
                            authorityKeyIdentifier = value;
                            break;

                        case FreshestCrlOid:
                            freshestCrlUris.AddRange(
                                CrlDistributionPoints
                                    .ReadPoints(value)
                                    .SelectMany(distributionPoint => distributionPoint.Uris)
                            );
                            break;

                        case IssuerAltNameOid:
                        case AuthorityInfoAccessOid:
                            break;

                        default:
                            if (critical)
                                hasUnsupportedFeatures = true;
                            break;
                    }
                }
            }

            if (hasCertificateIssuer && issuingDistributionPoint is not { IsIndirect: true })
                hasUnsupportedFeatures = true;

            tbs.ThrowIfNotEmpty();

            crl = new CertificateRevocationList
            {
                Issuer = issuer,
                ThisUpdate = thisUpdate,
                NextUpdate = nextUpdate,
                EntriesBySerialNumber = entries.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<CrlEntry>)pair.Value
                ),
                HasUnsupportedFeatures = hasUnsupportedFeatures,
                IssuingDistributionPoint = issuingDistributionPoint,
                CrlNumber = crlNumber,
                DeltaBaseCrlNumber = deltaBaseCrlNumber,
                AuthorityKeyIdentifier = authorityKeyIdentifier,
                FreshestCrlUris = freshestCrlUris,
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
    /// Finds the entry for the certificate that has the specified serial number and was issued by the specified
    /// authority, or returns <see langword="null"/> when the list holds none. The issuer of an entry is the one its
    /// certificate issuer extension names, which applies to the entries after it until another extension replaces it.
    /// </summary>
    public CrlEntry? FindEntry(BigInteger serialNumber, X500DistinguishedName certificateIssuer)
    {
        if (!EntriesBySerialNumber.TryGetValue(serialNumber, out var entries))
            return null;

        foreach (var entry in entries)
        {
            if (RevocationNames.Matches(entry.CertificateIssuer, certificateIssuer))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Determines whether the specified delta list can be applied to this complete list (RFC 5280 §5.2.4). The two
    /// lists must have the same issuer, the same scope and the same authority key, and the number of this list must
    /// be at least the number of the complete list the delta is based on and less than the number of the delta.
    /// </summary>
    public bool CanApplyDelta(CertificateRevocationList delta) =>
        !IsDelta
        && delta.DeltaBaseCrlNumber is { } baseNumber
        && delta.CrlNumber is { } deltaNumber
        && CrlNumber is { } number
        && number >= baseNumber
        && number < deltaNumber
        && RevocationNames.Matches(Issuer, delta.Issuer)
        && (IssuingDistributionPoint?.Encoded ?? ReadOnlyMemory<byte>.Empty).Span.SequenceEqual(
            (delta.IssuingDistributionPoint?.Encoded ?? ReadOnlyMemory<byte>.Empty).Span
        )
        && AuthorityKeyIdentifier.Span.SequenceEqual(delta.AuthorityKeyIdentifier.Span);

    /// <summary>
    /// Determines whether the list is within the scope it declares for a certificate at the specified distribution
    /// point (RFC 5280 §6.3.3 step b). A point that names a CRL issuer needs an indirect list issued by that
    /// authority; a point that names none needs a list issued by the certificate's issuer. The issuing distribution
    /// point of the list must then name one of the locations of the point, when it names any, and must allow the kind
    /// of certificate.
    /// </summary>
    public bool IsInScope(
        CrlDistributionPoint point,
        X500DistinguishedName certificateIssuer,
        bool isAuthority
    )
    {
        if (point.CrlIssuer is { } crlIssuer)
        {
            if (
                IssuingDistributionPoint is not { IsIndirect: true }
                || !RevocationNames.Matches(Issuer, crlIssuer)
            )
            {
                return false;
            }
        }
        else if (!RevocationNames.Matches(Issuer, certificateIssuer))
        {
            return false;
        }

        if (IssuingDistributionPoint is not { } idp)
            return true;

        if (idp.HasName && !idp.Uris.Any(uri => point.Uris.Contains(uri, StringComparer.Ordinal)))
            return false;

        if (idp.OnlyContainsUserCertificates && isAuthority)
            return false;

        return !(idp.OnlyContainsAuthorityCertificates && !isAuthority);
    }

    /// <summary>
    /// Gets the reasons the list covers for the specified distribution point (RFC 5280 §6.3.3 step d) as a mask: the
    /// reasons both the list and the point name, where a list or a point that names none covers every reason.
    /// </summary>
    public int GetCoveredReasons(CrlDistributionPoint point) =>
        (IssuingDistributionPoint?.OnlySomeReasons, point.Reasons) switch
        {
            ({ } listReasons, { } pointReasons) => listReasons & pointReasons,
            ({ } listReasons, null) => listReasons,
            (null, { } pointReasons) => pointReasons,
            _ => CrlReasons.All,
        } & CrlReasons.All;

    /// <summary>
    /// Determines whether the specified certificate issued and signed the list. The certificate must be allowed to
    /// sign revocation lists, its subject must match the issuer named in the list, and the signature must verify
    /// with the certificate's public key. The verdict for each signer is computed once per list.
    /// </summary>
    public bool IsSignedBy(X509Certificate2 signer) =>
        SignerVerdicts.GetOrAdd(
            signer.GetCertHashString(HashAlgorithmName.SHA256),
            _ => VerifySignedBy(signer)
        );

    private bool VerifySignedBy(X509Certificate2 issuer)
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

            // RSA PKCS#1 v1.5, RSASSA-PSS and ECDSA signatures with SHA-256, SHA-384 or SHA-512 are supported. A list
            // signed with SHA-1 is not accepted.
            return RevocationSignature.Verify(
                issuer,
                SignatureAlgorithm,
                SignatureAlgorithmParameters,
                SignedData.Span,
                Signature.Span
            );
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool TryReadIssuingDistributionPoint(
        byte[] value,
        [NotNullWhen(true)] out CrlIssuingDistributionPoint? point
    )
    {
        point = null;

        var reader = new AsnReader(value, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        reader.ThrowIfNotEmpty();

        var uris = new List<string>();
        var hasName = false;
        var onlyContainsUserCertificates = false;
        var onlyContainsAuthorityCertificates = false;
        int? onlySomeReasons = null;
        var isIndirect = false;

        while (sequence.HasData)
        {
            var tag = sequence.PeekTag();
            if (tag == DistributionPointNameTag)
            {
                hasName = true;
                var distributionPointName = sequence.ReadSequence(DistributionPointNameTag);
                if (!CrlDistributionPoints.TryReadFullNameUris(distributionPointName, uris))
                    return false;
            }
            else if (tag == OnlyContainsUserCertificatesTag)
            {
                onlyContainsUserCertificates = sequence.ReadBoolean(
                    OnlyContainsUserCertificatesTag
                );
            }
            else if (tag == OnlyContainsAuthorityCertificatesTag)
            {
                onlyContainsAuthorityCertificates = sequence.ReadBoolean(
                    OnlyContainsAuthorityCertificatesTag
                );
            }
            else if (tag == OnlySomeReasonsTag)
            {
                onlySomeReasons = CrlReasons.Read(sequence, OnlySomeReasonsTag);
            }
            else if (tag == IndirectCrlTag)
            {
                isIndirect = sequence.ReadBoolean(IndirectCrlTag);
            }
            else if (tag == OnlyContainsAttributeCertificatesTag)
            {
                if (sequence.ReadBoolean(OnlyContainsAttributeCertificatesTag))
                    return false;
            }
            else
            {
                sequence.ReadEncodedValue();
                return false;
            }
        }

        if (onlyContainsUserCertificates && onlyContainsAuthorityCertificates)
            return false;

        point = new CrlIssuingDistributionPoint(
            uris,
            hasName,
            onlyContainsUserCertificates,
            onlyContainsAuthorityCertificates,
            onlySomeReasons,
            isIndirect,
            value
        );
        return true;
    }

    private static bool IsTime(Asn1Tag tag) =>
        tag == Asn1Tag.UtcTime || tag == Asn1Tag.GeneralizedTime;

    private static BigInteger ReadInteger(byte[] value)
    {
        var reader = new AsnReader(value, AsnEncodingRules.DER);
        var number = reader.ReadInteger();
        reader.ThrowIfNotEmpty();
        return number;
    }

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
