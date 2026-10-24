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

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

/// <summary>
/// Builds the certificate revocation lists the CRL tests parse, byte by byte, so a test can include an extension or
/// a malformed field that <see cref="CertificateRevocationListBuilder"/> cannot produce. Every list is signed with an
/// ECDSA P-256 issuer key and SHA-256.
/// </summary>
internal static class TestCrls
{
    public const string IssuingDistributionPointOid = "2.5.29.28";
    public const string DeltaCrlIndicatorOid = "2.5.29.27";

    private const string EcdsaWithSha256Oid = "1.2.840.10045.4.3.2";

    public static byte[] Build(
        X509Certificate2 issuer,
        IReadOnlyList<byte[]>? revokedSerialNumbers = null,
        IReadOnlyList<(string Oid, bool Critical, byte[] Value)>? extensions = null,
        IReadOnlyList<(string Oid, bool Critical, byte[] Value)>? entryExtensions = null,
        DateTimeOffset? thisUpdate = null,
        DateTimeOffset? nextUpdate = null,
        bool omitNextUpdate = false
    )
    {
        var effectiveThisUpdate = thisUpdate ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        var effectiveNextUpdate = nextUpdate ?? DateTimeOffset.UtcNow.AddDays(1);

        var tbsWriter = new AsnWriter(AsnEncodingRules.DER);
        using (tbsWriter.PushSequence())
        {
            tbsWriter.WriteInteger(1);
            WriteAlgorithmIdentifier(tbsWriter);
            tbsWriter.WriteEncodedValue(issuer.SubjectName.RawData);
            tbsWriter.WriteUtcTime(effectiveThisUpdate);
            if (!omitNextUpdate)
                tbsWriter.WriteUtcTime(effectiveNextUpdate);

            if (revokedSerialNumbers is { Count: > 0 })
            {
                using (tbsWriter.PushSequence())
                {
                    foreach (var serialNumber in revokedSerialNumbers)
                    {
                        using (tbsWriter.PushSequence())
                        {
                            tbsWriter.WriteInteger(
                                new BigInteger(serialNumber, isUnsigned: true, isBigEndian: true)
                            );
                            tbsWriter.WriteUtcTime(effectiveThisUpdate);
                            if (entryExtensions is { Count: > 0 })
                                WriteExtensions(tbsWriter, entryExtensions);
                        }
                    }
                }
            }

            if (extensions is { Count: > 0 })
            {
                using (tbsWriter.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
                {
                    WriteExtensions(tbsWriter, extensions);
                }
            }
        }

        var tbs = tbsWriter.Encode();

        using var key =
            issuer.GetECDsaPrivateKey()
            ?? throw new InvalidOperationException("The issuer has no ECDSA private key.");
        var signature = key.SignData(
            tbs,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence
        );

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(tbs);
            WriteAlgorithmIdentifier(writer);
            writer.WriteBitString(signature);
        }

        return writer.Encode();
    }

    public static byte[] IssuingDistributionPoint(
        string? uri = null,
        bool onlyContainsUserCertificates = false,
        bool onlyContainsAuthorityCertificates = false,
        bool indirect = false
    )
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            if (uri is not null)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
                {
                    using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
                    {
                        writer.WriteCharacterString(
                            UniversalTagNumber.IA5String,
                            uri,
                            new Asn1Tag(TagClass.ContextSpecific, 6)
                        );
                    }
                }
            }

            if (onlyContainsUserCertificates)
                writer.WriteBoolean(true, new Asn1Tag(TagClass.ContextSpecific, 1));

            if (onlyContainsAuthorityCertificates)
                writer.WriteBoolean(true, new Asn1Tag(TagClass.ContextSpecific, 2));

            if (indirect)
                writer.WriteBoolean(true, new Asn1Tag(TagClass.ContextSpecific, 4));
        }

        return writer.Encode();
    }

    private static void WriteAlgorithmIdentifier(AsnWriter writer)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(EcdsaWithSha256Oid);
        }
    }

    private static void WriteExtensions(
        AsnWriter writer,
        IReadOnlyList<(string Oid, bool Critical, byte[] Value)> extensions
    )
    {
        using (writer.PushSequence())
        {
            foreach (var (oid, critical, value) in extensions)
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(oid);
                    if (critical)
                        writer.WriteBoolean(true);
                    writer.WriteOctetString(value);
                }
            }
        }
    }
}
