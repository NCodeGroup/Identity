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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Reads the locations of the certificate revocation lists a certificate names in its CRL distribution points
/// extension.
/// </summary>
internal static class CrlDistributionPoints
{
    private const string ExtensionOid = "2.5.29.31";
    private const string FreshestCrlOid = "2.5.29.46";

    private static readonly Asn1Tag DistributionPointNameTag = new(
        TagClass.ContextSpecific,
        0,
        true
    );

    private static readonly Asn1Tag ReasonsTag = new(TagClass.ContextSpecific, 1);

    private static readonly Asn1Tag CrlIssuerTag = new(TagClass.ContextSpecific, 2, true);

    private static readonly Asn1Tag FullNameTag = new(TagClass.ContextSpecific, 0, true);

    private static readonly Asn1Tag DirectoryNameTag = new(TagClass.ContextSpecific, 4, true);

    private static readonly Asn1Tag UniformResourceIdentifierTag = new(TagClass.ContextSpecific, 6);

    /// <summary>
    /// Reads the distribution points that the certificate names in its CRL distribution points extension. A point
    /// without an <c>http</c> or <c>https</c> location is not returned, and a certificate without the extension or
    /// with a malformed one yields no points.
    /// </summary>
    public static IReadOnlyList<CrlDistributionPoint> Read(X509Certificate2 certificate) =>
        ReadExtension(certificate, ExtensionOid);

    /// <summary>
    /// Reads the distribution points that the certificate names in its freshest CRL extension, which names the
    /// locations of delta lists. The syntax and the rules are those of <see cref="Read"/>.
    /// </summary>
    public static IReadOnlyList<CrlDistributionPoint> ReadFreshest(X509Certificate2 certificate) =>
        ReadExtension(certificate, FreshestCrlOid);

    /// <summary>
    /// Reads the distribution points of an encoded <c>CRLDistributionPoints</c> value, the value of a CRL distribution
    /// points extension or of a freshest CRL extension. A point without an <c>http</c> or <c>https</c> location, or
    /// whose CRL issuer names no directory name, is not returned. A malformed value yields no points.
    /// </summary>
    public static IReadOnlyList<CrlDistributionPoint> ReadPoints(ReadOnlyMemory<byte> value)
    {
        var result = new List<CrlDistributionPoint>();
        try
        {
            var reader = new AsnReader(value, AsnEncodingRules.DER);
            var points = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            while (points.HasData)
            {
                var point = points.ReadSequence();
                var uris = new List<string>();
                int? reasons = null;
                X500DistinguishedName? crlIssuer = null;
                var usable = true;

                if (point.HasData && point.PeekTag() == DistributionPointNameTag)
                {
                    var distributionPointName = point.ReadSequence(DistributionPointNameTag);
                    TryReadFullNameUris(distributionPointName, uris);
                }

                if (point.HasData && point.PeekTag() == ReasonsTag)
                    reasons = CrlReasons.Read(point, ReasonsTag);

                if (point.HasData && point.PeekTag() == CrlIssuerTag)
                {
                    if (TryReadDirectoryName(point.ReadSequence(CrlIssuerTag), out var issuer))
                        crlIssuer = issuer;
                    else
                        usable = false;
                }

                point.ThrowIfNotEmpty();

                if (usable && uris.Count > 0)
                    result.Add(new CrlDistributionPoint(uris, reasons, crlIssuer));
            }
        }
        catch (AsnContentException)
        {
            return [];
        }
        catch (CryptographicException)
        {
            return [];
        }

        return result;
    }

    /// <summary>
    /// Reads the first directory name of a list of general names. The reader is positioned inside the sequence of
    /// general names. Returns <see langword="false"/> when the list holds no directory name.
    /// </summary>
    public static bool TryReadDirectoryName(
        AsnReader generalNames,
        [NotNullWhen(true)] out X500DistinguishedName? name
    )
    {
        while (generalNames.HasData)
        {
            if (generalNames.PeekTag() != DirectoryNameTag)
            {
                generalNames.ReadEncodedValue();
                continue;
            }

            var wrapper = generalNames.ReadSequence(DirectoryNameTag);
            name = new X500DistinguishedName(wrapper.ReadEncodedValue().Span);
            wrapper.ThrowIfNotEmpty();
            return true;
        }

        name = null;
        return false;
    }

    /// <summary>
    /// Reads the <c>http</c> and <c>https</c> locations of a distribution point name that is a full name. The reader is
    /// positioned inside the tag that holds the name. Returns <see langword="false"/>, and adds nothing, when the name
    /// is relative to the CRL issuer.
    /// </summary>
    public static bool TryReadFullNameUris(AsnReader distributionPointName, List<string> uris)
    {
        if (distributionPointName.PeekTag() != FullNameTag)
        {
            distributionPointName.ReadEncodedValue();
            return false;
        }

        var generalNames = distributionPointName.ReadSequence(FullNameTag);
        while (generalNames.HasData)
        {
            if (generalNames.PeekTag() != UniformResourceIdentifierTag)
            {
                generalNames.ReadEncodedValue();
                continue;
            }

            var value = generalNames.ReadCharacterString(
                UniversalTagNumber.IA5String,
                UniformResourceIdentifierTag
            );
            if (
                Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            )
            {
                uris.Add(value);
            }
        }

        return true;
    }

    private static IReadOnlyList<CrlDistributionPoint> ReadExtension(
        X509Certificate2 certificate,
        string oid
    )
    {
        var extension = certificate.Extensions[oid];
        return extension is null ? [] : ReadPoints(extension.RawData);
    }
}
