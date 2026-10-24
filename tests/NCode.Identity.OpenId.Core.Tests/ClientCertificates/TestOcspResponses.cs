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
using NCode.Identity.OpenId.Authentication.ClientCertificates;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

/// <summary>
/// Builds the OCSP responses the OCSP tests parse, byte by byte, so a test can include an extension, a responder
/// identification or a malformed field that a real responder would not produce. Every response is signed with an ECDSA
/// P-256 signer key and SHA-256.
/// </summary>
internal static class TestOcspResponses
{
    public const string NonceOid = "1.3.6.1.5.5.7.48.1.2";

    private const string BasicResponseTypeOid = "1.3.6.1.5.5.7.48.1.1";
    private const string EcdsaWithSha256Oid = "1.2.840.10045.4.3.2";

    private static readonly Asn1Tag ExplicitZeroTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag ExplicitOneTag = new(TagClass.ContextSpecific, 1, true);
    private static readonly Asn1Tag ExplicitTwoTag = new(TagClass.ContextSpecific, 2, true);

    public static byte[] Build(
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        X509Certificate2 signer,
        ClientCertificateRevocationStatus status = ClientCertificateRevocationStatus.Good,
        DateTimeOffset? thisUpdate = null,
        DateTimeOffset? nextUpdate = null,
        bool omitNextUpdate = false,
        bool responderByKey = false,
        IReadOnlyList<X509Certificate2>? certificates = null,
        IReadOnlyList<(string Oid, bool Critical, byte[] Value)>? responseExtensions = null,
        IReadOnlyList<(string Oid, bool Critical, byte[] Value)>? singleExtensions = null,
        byte[]? serialNumber = null
    )
    {
        var effectiveThisUpdate = thisUpdate ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        var effectiveNextUpdate = nextUpdate ?? DateTimeOffset.UtcNow.AddDays(1);

        var tbsWriter = new AsnWriter(AsnEncodingRules.DER);
        using (tbsWriter.PushSequence())
        {
            if (responderByKey)
            {
                using (tbsWriter.PushSequence(ExplicitTwoTag))
                {
                    tbsWriter.WriteOctetString(
                        CryptographicOperations.HashData(
                            HashAlgorithmName.SHA1,
                            signer.PublicKey.EncodedKeyValue.RawData
                        )
                    );
                }
            }
            else
            {
                using (tbsWriter.PushSequence(ExplicitOneTag))
                {
                    tbsWriter.WriteEncodedValue(signer.SubjectName.RawData);
                }
            }

            tbsWriter.WriteGeneralizedTime(effectiveThisUpdate, omitFractionalSeconds: true);

            using (tbsWriter.PushSequence())
            {
                using (tbsWriter.PushSequence())
                {
                    WriteCertificateId(tbsWriter, certificate, issuer, serialNumber);
                    WriteStatus(tbsWriter, status, effectiveThisUpdate);
                    tbsWriter.WriteGeneralizedTime(
                        effectiveThisUpdate,
                        omitFractionalSeconds: true
                    );
                    if (!omitNextUpdate)
                    {
                        using (tbsWriter.PushSequence(ExplicitZeroTag))
                        {
                            tbsWriter.WriteGeneralizedTime(
                                effectiveNextUpdate,
                                omitFractionalSeconds: true
                            );
                        }
                    }

                    if (singleExtensions is { Count: > 0 })
                    {
                        using (tbsWriter.PushSequence(ExplicitOneTag))
                        {
                            WriteExtensions(tbsWriter, singleExtensions);
                        }
                    }
                }
            }

            if (responseExtensions is { Count: > 0 })
            {
                using (tbsWriter.PushSequence(ExplicitOneTag))
                {
                    WriteExtensions(tbsWriter, responseExtensions);
                }
            }
        }

        var tbs = tbsWriter.Encode();

        using var key =
            signer.GetECDsaPrivateKey()
            ?? throw new InvalidOperationException("The signer has no ECDSA private key.");
        var signature = key.SignData(
            tbs,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence
        );

        var basicWriter = new AsnWriter(AsnEncodingRules.DER);
        using (basicWriter.PushSequence())
        {
            basicWriter.WriteEncodedValue(tbs);
            using (basicWriter.PushSequence())
            {
                basicWriter.WriteObjectIdentifier(EcdsaWithSha256Oid);
            }

            basicWriter.WriteBitString(signature);
            if (certificates is { Count: > 0 })
            {
                using (basicWriter.PushSequence(ExplicitZeroTag))
                {
                    using (basicWriter.PushSequence())
                    {
                        foreach (var included in certificates)
                            basicWriter.WriteEncodedValue(included.RawData);
                    }
                }
            }
        }

        var basic = basicWriter.Encode();

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue([0x0A, 0x01, 0x00]);
            using (writer.PushSequence(ExplicitZeroTag))
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(BasicResponseTypeOid);
                    writer.WriteOctetString(basic);
                }
            }
        }

        return writer.Encode();
    }

    public static byte[] Unsuccessful(int responseStatus)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue([0x0A, 0x01, (byte)responseStatus]);
        }

        return writer.Encode();
    }

    private static void WriteCertificateId(
        AsnWriter writer,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        byte[]? serialNumber
    )
    {
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.3.14.3.2.26");
                writer.WriteNull();
            }

            writer.WriteOctetString(
                CryptographicOperations.HashData(
                    HashAlgorithmName.SHA1,
                    certificate.IssuerName.RawData
                )
            );
            writer.WriteOctetString(
                CryptographicOperations.HashData(
                    HashAlgorithmName.SHA1,
                    issuer.PublicKey.EncodedKeyValue.RawData
                )
            );
            writer.WriteInteger(
                new BigInteger(
                    serialNumber ?? certificate.SerialNumberBytes.ToArray(),
                    isUnsigned: false,
                    isBigEndian: true
                )
            );
        }
    }

    private static void WriteStatus(
        AsnWriter writer,
        ClientCertificateRevocationStatus status,
        DateTimeOffset revocationTime
    )
    {
        switch (status)
        {
            case ClientCertificateRevocationStatus.Good:
                writer.WriteNull(new Asn1Tag(TagClass.ContextSpecific, 0));
                break;

            case ClientCertificateRevocationStatus.Revoked:
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true)))
                {
                    writer.WriteGeneralizedTime(revocationTime, omitFractionalSeconds: true);
                }

                break;

            default:
                writer.WriteNull(new Asn1Tag(TagClass.ContextSpecific, 2));
                break;
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
