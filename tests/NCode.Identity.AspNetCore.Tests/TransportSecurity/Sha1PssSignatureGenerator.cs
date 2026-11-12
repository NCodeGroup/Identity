// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.AspNetCore.Tests.TransportSecurity;

/// <summary>
/// Signs with RSASSA-PSS, SHA-1 as the hash and as the MGF1 hash, and a salt length of 20, which the transport
/// certificate inspector must refuse. The framework's signature generator does not sign with SHA-1.
/// </summary>
internal sealed class Sha1PssSignatureGenerator(RSA rsa) : X509SignatureGenerator
{
    private const string RsaSsaPssOid = "1.2.840.113549.1.1.10";
    private const string Mgf1Oid = "1.2.840.113549.1.1.8";
    private const string Sha1Oid = "1.3.14.3.2.26";
    private const int SaltLength = 20;

    protected override PublicKey BuildPublicKey() =>
        CreateForRSA(rsa, RSASignaturePadding.Pkcs1).PublicKey;

    // AlgorithmIdentifier { id-RSASSA-PSS, RSASSA-PSS-params { [0] sha1, [1] mgf1(sha1), [2] saltLength } }.
    public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(RsaSsaPssOid);
            using (writer.PushSequence())
            {
                using (
                    writer.PushSequence(
                        new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)
                    )
                )
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(Sha1Oid);
                    }
                }

                using (
                    writer.PushSequence(
                        new Asn1Tag(TagClass.ContextSpecific, 1, isConstructed: true)
                    )
                )
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(Mgf1Oid);
                        using (writer.PushSequence())
                        {
                            writer.WriteObjectIdentifier(Sha1Oid);
                        }
                    }
                }

                using (
                    writer.PushSequence(
                        new Asn1Tag(TagClass.ContextSpecific, 2, isConstructed: true)
                    )
                )
                {
                    writer.WriteInteger(SaltLength);
                }
            }
        }

        return writer.Encode();
    }

    public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) =>
        rsa.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pss);
}
