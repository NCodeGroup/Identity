// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Asn1;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Encodes the OCSP requests (RFC 6960 §4.1) that the server sends to a responder.
/// </summary>
internal static class OcspRequest
{
    /// <summary>
    /// The media type of an OCSP request sent in the body of an HTTP <c>POST</c> request.
    /// </summary>
    public const string ContentType = "application/ocsp-request";

    /// <summary>
    /// Encodes a request for one certificate: no version, requestor name, nonce or signature.
    /// </summary>
    public static byte[] Encode(OcspCertificateId certificateId)
    {
        // OCSPRequest ::= SEQUENCE { tbsRequest TBSRequest }
        // TBSRequest  ::= SEQUENCE { requestList SEQUENCE OF Request }
        // Request     ::= SEQUENCE { reqCert CertID }
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSequence())
        using (writer.PushSequence())
        using (writer.PushSequence())
        {
            certificateId.WriteTo(writer);
        }

        return writer.Encode();
    }
}
