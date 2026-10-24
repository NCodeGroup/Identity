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
/// A parsed OCSP response (RFC 6960 §4.2) of the basic response type: the status of the response, the answers for
/// the certificates it covers, and the responder's identity, signature and certificates. Parsing reads the response;
/// it does not verify who signed it.
/// </summary>
internal sealed class OcspResponse
{
    private const string BasicResponseTypeOid = "1.3.6.1.5.5.7.48.1.1";
    private const string OcspNoCheckOid = "1.3.6.1.5.5.7.48.1.5";
    private const string OcspSigningOid = "1.3.6.1.5.5.7.3.9";

    private static readonly Asn1Tag ExplicitZeroTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag ExplicitOneTag = new(TagClass.ContextSpecific, 1, true);
    private static readonly Asn1Tag ExplicitTwoTag = new(TagClass.ContextSpecific, 2, true);
    private static readonly Asn1Tag GoodTag = new(TagClass.ContextSpecific, 0);
    private static readonly Asn1Tag UnknownTag = new(TagClass.ContextSpecific, 2);

    /// <summary>
    /// Gets the status of the response: <c>0</c> successful, <c>1</c> malformed request, <c>2</c> internal error,
    /// <c>3</c> try later, <c>5</c> signature required and <c>6</c> unauthorized. A response whose status is not
    /// successful carries nothing else.
    /// </summary>
    public required int ResponseStatus { get; init; }

    /// <summary>
    /// Gets the name of the responder, or <see langword="null"/> when the response identifies the responder by key.
    /// </summary>
    public X500DistinguishedName? ResponderName { get; init; }

    /// <summary>
    /// Gets the SHA-1 hash of the public key of the responder, or an empty value when the response identifies the
    /// responder by name.
    /// </summary>
    public ReadOnlyMemory<byte> ResponderKeyHash { get; init; }

    /// <summary>
    /// Gets the time at which the responder signed the response.
    /// </summary>
    public DateTimeOffset ProducedAt { get; init; }

    /// <summary>
    /// Gets the answers of the response, one for each certificate it covers.
    /// </summary>
    public IReadOnlyList<OcspSingleResponse> Responses { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the response carries a critical extension, which this reader does not support,
    /// so that the response must not be relied on.
    /// </summary>
    public bool HasUnsupportedFeatures { get; init; }

    /// <summary>
    /// Gets the object identifier of the algorithm that signed the response.
    /// </summary>
    public string SignatureAlgorithm { get; init; } = string.Empty;

    /// <summary>
    /// Gets the encoded parameters of the algorithm that signed the response, or an empty value when the algorithm
    /// carries none.
    /// </summary>
    public ReadOnlyMemory<byte> SignatureAlgorithmParameters { get; init; }

    /// <summary>
    /// Gets the encoded bytes the signature covers.
    /// </summary>
    public ReadOnlyMemory<byte> SignedData { get; init; }

    /// <summary>
    /// Gets the signature over the signed bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Signature { get; init; }

    /// <summary>
    /// Gets the encoded certificates that the response carries, which can include the certificate of a delegated
    /// responder.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> Certificates { get; init; } = [];

    /// <summary>
    /// Reads a DER-encoded OCSP response. Returns <see langword="false"/> when the bytes are not a well-formed response
    /// of the basic response type. A response whose status is not successful is returned with that status alone.
    /// </summary>
    public static bool TryParse(
        ReadOnlyMemory<byte> encoded,
        [NotNullWhen(true)] out OcspResponse? response
    )
    {
        response = null;

        try
        {
            // OCSPResponse ::= SEQUENCE { responseStatus ENUMERATED,
            //                             responseBytes [0] EXPLICIT ResponseBytes OPTIONAL }
            var reader = new AsnReader(encoded, AsnEncodingRules.DER);
            var ocspResponse = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            var statusBytes = ocspResponse.ReadEnumeratedBytes();
            if (statusBytes.Length != 1)
                return false;

            var responseStatus = statusBytes.Span[0];
            if (responseStatus != 0)
            {
                response = new OcspResponse { ResponseStatus = responseStatus };
                return true;
            }

            if (!ocspResponse.HasData)
                return false;

            // ResponseBytes ::= SEQUENCE { responseType OBJECT IDENTIFIER, response OCTET STRING }
            var explicitResponseBytes = ocspResponse.ReadSequence(ExplicitZeroTag);
            ocspResponse.ThrowIfNotEmpty();
            var responseBytes = explicitResponseBytes.ReadSequence();
            explicitResponseBytes.ThrowIfNotEmpty();
            if (responseBytes.ReadObjectIdentifier() != BasicResponseTypeOid)
                return false;

            var basicEncoded = responseBytes.ReadOctetString();
            responseBytes.ThrowIfNotEmpty();

            // BasicOCSPResponse ::= SEQUENCE { tbsResponseData ResponseData, signatureAlgorithm AlgorithmIdentifier,
            //                                  signature BIT STRING,
            //                                  certs [0] EXPLICIT SEQUENCE OF Certificate OPTIONAL }
            var basicReader = new AsnReader(basicEncoded, AsnEncodingRules.DER);
            var basic = basicReader.ReadSequence();
            basicReader.ThrowIfNotEmpty();

            var signedData = basic.ReadEncodedValue();

            var algorithm = basic.ReadSequence();
            var signatureAlgorithm = algorithm.ReadObjectIdentifier();
            var signatureParameters = algorithm.HasData
                ? algorithm.ReadEncodedValue()
                : ReadOnlyMemory<byte>.Empty;
            algorithm.ThrowIfNotEmpty();

            var signature = basic.ReadBitString(out var unusedBitCount);
            if (unusedBitCount != 0)
                return false;

            var certificates = new List<ReadOnlyMemory<byte>>();
            if (basic.HasData && basic.PeekTag() == ExplicitZeroTag)
            {
                var explicitCertificates = basic.ReadSequence(ExplicitZeroTag);
                var certificateSequence = explicitCertificates.ReadSequence();
                explicitCertificates.ThrowIfNotEmpty();
                while (certificateSequence.HasData)
                    certificates.Add(certificateSequence.ReadEncodedValue());
            }

            basic.ThrowIfNotEmpty();

            // ResponseData ::= SEQUENCE { version [0] EXPLICIT INTEGER DEFAULT v1, responderID ResponderID,
            //                             producedAt GeneralizedTime, responses SEQUENCE OF SingleResponse,
            //                             responseExtensions [1] EXPLICIT Extensions OPTIONAL }
            var tbs = new AsnReader(signedData, AsnEncodingRules.DER).ReadSequence();

            if (tbs.PeekTag() == ExplicitZeroTag)
            {
                var explicitVersion = tbs.ReadSequence(ExplicitZeroTag);
                var version = explicitVersion.ReadInteger();
                explicitVersion.ThrowIfNotEmpty();
                if (!version.IsZero)
                    return false;
            }

            X500DistinguishedName? responderName = null;
            var responderKeyHash = ReadOnlyMemory<byte>.Empty;
            var responderTag = tbs.PeekTag();
            if (responderTag == ExplicitOneTag)
            {
                var explicitName = tbs.ReadSequence(ExplicitOneTag);
                responderName = new X500DistinguishedName(explicitName.ReadEncodedValue().Span);
                explicitName.ThrowIfNotEmpty();
            }
            else if (responderTag == ExplicitTwoTag)
            {
                var explicitKeyHash = tbs.ReadSequence(ExplicitTwoTag);
                responderKeyHash = explicitKeyHash.ReadOctetString();
                explicitKeyHash.ThrowIfNotEmpty();
            }
            else
            {
                return false;
            }

            var producedAt = tbs.ReadGeneralizedTime();

            var responses = new List<OcspSingleResponse>();
            var singleResponses = tbs.ReadSequence();
            while (singleResponses.HasData)
                responses.Add(ReadSingleResponse(singleResponses));

            var hasUnsupportedFeatures = false;
            if (tbs.HasData && tbs.PeekTag() == ExplicitOneTag)
                hasUnsupportedFeatures = HasCriticalExtension(tbs);

            tbs.ThrowIfNotEmpty();

            response = new OcspResponse
            {
                ResponseStatus = responseStatus,
                ResponderName = responderName,
                ResponderKeyHash = responderKeyHash,
                ProducedAt = producedAt,
                Responses = responses,
                HasUnsupportedFeatures = hasUnsupportedFeatures,
                SignatureAlgorithm = signatureAlgorithm,
                SignatureAlgorithmParameters = signatureParameters,
                SignedData = signedData,
                Signature = signature,
                Certificates = certificates,
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
    /// Finds the answer for the certificate that the specified identifier names, or returns <see langword="null"/>
    /// when the response holds none.
    /// </summary>
    public OcspSingleResponse? FindResponse(OcspCertificateId certificateId) =>
        Responses.FirstOrDefault(single => single.CertificateId.Matches(certificateId));

    /// <summary>
    /// Determines whether the response was signed by the issuer of the certificate, or by a responder that the issuer
    /// authorized (RFC 6960 §4.2.2.2). A delegated responder is a certificate the response carries, which the
    /// response's responder identification names, which the issuer signed, which allows OCSP signing and which is valid
    /// at the specified time.
    /// </summary>
    /// <param name="issuer">The issuer of the certificate the response answers for.</param>
    /// <param name="now">The time at which a delegated responder must be valid.</param>
    /// <param name="uncheckedResponder">
    /// Receives the encoded certificate of a delegated responder that lacks the <c>id-pkix-ocsp-nocheck</c> extension,
    /// whose own revocation the caller checks; otherwise receives an empty value.
    /// </param>
    public bool TryVerifySignature(
        X509Certificate2 issuer,
        DateTimeOffset now,
        out ReadOnlyMemory<byte> uncheckedResponder
    )
    {
        uncheckedResponder = ReadOnlyMemory<byte>.Empty;

        if (IdentifiesResponder(issuer) && VerifiedBy(issuer))
            return true;

        foreach (var encoded in Certificates)
        {
            X509Certificate2 responder;
            try
            {
                responder = X509CertificateLoader.LoadCertificate(encoded.Span);
            }
            catch (CryptographicException)
            {
                continue;
            }

            using (responder)
            {
                if (!IsAuthorizedResponder(responder, issuer, now) || !VerifiedBy(responder))
                    continue;

                if (responder.Extensions[OcspNoCheckOid] is null)
                    uncheckedResponder = encoded;

                return true;
            }
        }

        return false;
    }

    private bool VerifiedBy(X509Certificate2 signer) =>
        RevocationSignature.Verify(
            signer,
            SignatureAlgorithm,
            SignatureAlgorithmParameters,
            SignedData.Span,
            Signature.Span
        );

    private bool IdentifiesResponder(X509Certificate2 certificate) =>
        ResponderName is { } name
            ? RevocationNames.Matches(name, certificate.SubjectName)
            : ResponderKeyHash.Span.SequenceEqual(
                CryptographicOperations.HashData(
                    HashAlgorithmName.SHA1,
                    certificate.PublicKey.EncodedKeyValue.RawData
                )
            );

    private bool IsAuthorizedResponder(
        X509Certificate2 responder,
        X509Certificate2 issuer,
        DateTimeOffset now
    ) =>
        IdentifiesResponder(responder)
        && RevocationNames.Matches(responder.IssuerName, issuer.SubjectName)
        && RevocationSignature.VerifyCertificateSignature(responder, issuer)
        && HasOcspSigningUsage(responder)
        && new DateTimeOffset(responder.NotBefore) <= now
        && now <= new DateTimeOffset(responder.NotAfter);

    private static bool HasOcspSigningUsage(X509Certificate2 responder) =>
        responder
            .Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Any(extension =>
                extension.EnhancedKeyUsages.OfType<Oid>().Any(oid => oid.Value == OcspSigningOid)
            );

    private static OcspSingleResponse ReadSingleResponse(AsnReader responses)
    {
        // SingleResponse ::= SEQUENCE { certID CertID, certStatus CertStatus, thisUpdate GeneralizedTime,
        //                               nextUpdate [0] EXPLICIT GeneralizedTime OPTIONAL,
        //                               singleExtensions [1] EXPLICIT Extensions OPTIONAL }
        var single = responses.ReadSequence();
        var certificateId = OcspCertificateId.Read(single);

        var statusTag = single.PeekTag();
        ClientCertificateRevocationStatus status;
        if (statusTag == GoodTag)
        {
            single.ReadNull(GoodTag);
            status = ClientCertificateRevocationStatus.Good;
        }
        else if (statusTag == ExplicitOneTag)
        {
            // RevokedInfo ::= SEQUENCE { revocationTime GeneralizedTime,
            //                            revocationReason [0] EXPLICIT CRLReason OPTIONAL }
            var revoked = single.ReadSequence(ExplicitOneTag);
            revoked.ReadGeneralizedTime();
            if (revoked.HasData)
            {
                var explicitReason = revoked.ReadSequence(ExplicitZeroTag);
                explicitReason.ReadEnumeratedBytes();
                explicitReason.ThrowIfNotEmpty();
            }

            revoked.ThrowIfNotEmpty();
            status = ClientCertificateRevocationStatus.Revoked;
        }
        else if (statusTag == UnknownTag)
        {
            single.ReadNull(UnknownTag);
            status = ClientCertificateRevocationStatus.Unknown;
        }
        else
        {
            throw new AsnContentException();
        }

        var thisUpdate = single.ReadGeneralizedTime();

        DateTimeOffset? nextUpdate = null;
        if (single.HasData && single.PeekTag() == ExplicitZeroTag)
        {
            var explicitNextUpdate = single.ReadSequence(ExplicitZeroTag);
            nextUpdate = explicitNextUpdate.ReadGeneralizedTime();
            explicitNextUpdate.ThrowIfNotEmpty();
        }

        var hasUnsupportedFeatures = false;
        if (single.HasData && single.PeekTag() == ExplicitOneTag)
            hasUnsupportedFeatures = HasCriticalExtension(single);

        single.ThrowIfNotEmpty();

        return new OcspSingleResponse
        {
            CertificateId = certificateId,
            Status = status,
            ThisUpdate = thisUpdate,
            NextUpdate = nextUpdate,
            HasUnsupportedFeatures = hasUnsupportedFeatures,
        };
    }

    // Reads [1] EXPLICIT Extensions and reports whether any extension is critical. A critical extension is not
    // supported; the nonce extension is non-critical and is therefore ignored.
    private static bool HasCriticalExtension(AsnReader reader)
    {
        var explicitExtensions = reader.ReadSequence(ExplicitOneTag);
        var extensions = explicitExtensions.ReadSequence();
        explicitExtensions.ThrowIfNotEmpty();

        var hasCritical = false;
        while (extensions.HasData)
        {
            var extension = extensions.ReadSequence();
            extension.ReadObjectIdentifier();
            if (extension.PeekTag() == Asn1Tag.Boolean && extension.ReadBoolean())
                hasCritical = true;
            extension.ReadOctetString();
            extension.ThrowIfNotEmpty();
        }

        return hasCritical;
    }
}
