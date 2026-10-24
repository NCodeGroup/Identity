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
/// Identifies a certificate to an OCSP responder (RFC 6960 §4.1.1): the hashes of the issuer's name and public key, and
/// the serial number of the certificate.
/// </summary>
internal sealed class OcspCertificateId
{
    /// <summary>
    /// The object identifier of the SHA-1 hash algorithm, the one hash algorithm that is requested.
    /// </summary>
    public const string Sha1Oid = "1.3.14.3.2.26";

    /// <summary>
    /// Gets the object identifier of the hash algorithm that produced the issuer hashes.
    /// </summary>
    public required string HashAlgorithm { get; init; }

    /// <summary>
    /// Gets the hash of the distinguished name of the issuer.
    /// </summary>
    public required ReadOnlyMemory<byte> IssuerNameHash { get; init; }

    /// <summary>
    /// Gets the hash of the public key of the issuer.
    /// </summary>
    public required ReadOnlyMemory<byte> IssuerKeyHash { get; init; }

    /// <summary>
    /// Gets the serial number of the certificate.
    /// </summary>
    public required BigInteger SerialNumber { get; init; }

    /// <summary>
    /// Creates the identifier of a certificate from the certificate and its issuer.
    /// </summary>
    public static OcspCertificateId Create(X509Certificate2 certificate, X509Certificate2 issuer) =>
        new()
        {
            // RFC 5019 §2.1.1 requires SHA-1 here. It only identifies the issuer; the response is trusted by its
            // signature.
            HashAlgorithm = Sha1Oid,
            IssuerNameHash = CryptographicOperations.HashData(
                HashAlgorithmName.SHA1,
                certificate.IssuerName.RawData
            ),
            IssuerKeyHash = CryptographicOperations.HashData(
                HashAlgorithmName.SHA1,
                issuer.PublicKey.EncodedKeyValue.RawData
            ),
            SerialNumber = new BigInteger(
                certificate.SerialNumberBytes.Span,
                isUnsigned: false,
                isBigEndian: true
            ),
        };

    /// <summary>
    /// Reads one <c>CertID</c> sequence, whose hash algorithm has no parameters or the parameters <c>NULL</c>.
    /// </summary>
    /// <exception cref="AsnContentException">The bytes are not a well-formed <c>CertID</c>.</exception>
    public static OcspCertificateId Read(AsnReader reader)
    {
        // CertID ::= SEQUENCE { hashAlgorithm AlgorithmIdentifier, issuerNameHash OCTET STRING,
        //                       issuerKeyHash OCTET STRING, serialNumber INTEGER }
        var sequence = reader.ReadSequence();

        var algorithm = sequence.ReadSequence();
        var hashAlgorithm = algorithm.ReadObjectIdentifier();
        if (algorithm.HasData)
            algorithm.ReadNull();
        algorithm.ThrowIfNotEmpty();

        var issuerNameHash = sequence.ReadOctetString();
        var issuerKeyHash = sequence.ReadOctetString();
        var serialNumber = sequence.ReadInteger();
        sequence.ThrowIfNotEmpty();

        return new OcspCertificateId
        {
            HashAlgorithm = hashAlgorithm,
            IssuerNameHash = issuerNameHash,
            IssuerKeyHash = issuerKeyHash,
            SerialNumber = serialNumber,
        };
    }

    /// <summary>
    /// Writes the identifier as <c>SEQUENCE { SEQUENCE { OID, NULL }, OCTET STRING, OCTET STRING, INTEGER }</c>.
    /// </summary>
    public void WriteTo(AsnWriter writer)
    {
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(HashAlgorithm);
                writer.WriteNull();
            }

            writer.WriteOctetString(IssuerNameHash.Span);
            writer.WriteOctetString(IssuerKeyHash.Span);
            writer.WriteInteger(SerialNumber);
        }
    }

    /// <summary>
    /// Determines whether the specified identifier names the same certificate of the same issuer.
    /// </summary>
    public bool Matches(OcspCertificateId other) =>
        string.Equals(HashAlgorithm, other.HashAlgorithm, StringComparison.Ordinal)
        && IssuerNameHash.Span.SequenceEqual(other.IssuerNameHash.Span)
        && IssuerKeyHash.Span.SequenceEqual(other.IssuerKeyHash.Span)
        && SerialNumber == other.SerialNumber;
}
