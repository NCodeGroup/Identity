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
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Reads the locations of the certificate revocation lists a certificate names in its CRL distribution points
/// extension.
/// </summary>
internal static class CrlDistributionPoints
{
    private const string ExtensionOid = "2.5.29.31";

    private static readonly Asn1Tag DistributionPointNameTag = new(
        TagClass.ContextSpecific,
        0,
        true
    );

    private static readonly Asn1Tag FullNameTag = new(TagClass.ContextSpecific, 0, true);

    private static readonly Asn1Tag UniformResourceIdentifierTag = new(TagClass.ContextSpecific, 6);

    /// <summary>
    /// Gets the <c>http</c> and <c>https</c> locations, as written, that the certificate names as the full name of a
    /// distribution point, in the order they appear. A distribution point that names a reason or a separate CRL
    /// issuer contributes nothing, and a malformed extension yields no locations.
    /// </summary>
    public static IReadOnlyList<string> GetUris(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions[ExtensionOid];
        if (extension is null)
            return [];

        var uris = new List<string>();
        try
        {
            var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
            var points = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            while (points.HasData)
            {
                var point = points.ReadSequence();
                var pointUris = new List<string>();
                var usable = true;

                while (point.HasData)
                {
                    if (point.PeekTag() == DistributionPointNameTag)
                    {
                        var distributionPointName = point.ReadSequence(DistributionPointNameTag);
                        TryReadFullNameUris(distributionPointName, pointUris);
                    }
                    else
                    {
                        // reasons and cRLIssuer make the point unusable
                        point.ReadEncodedValue();
                        usable = false;
                    }
                }

                if (usable)
                    uris.AddRange(pointUris);
            }
        }
        catch (AsnContentException)
        {
            return [];
        }

        return uris;
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
}
