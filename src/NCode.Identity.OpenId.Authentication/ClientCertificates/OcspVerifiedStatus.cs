// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// The status an OCSP response reports for a certificate, after the response was parsed, found current and verified
/// as signed by the issuer or by a responder the issuer authorized.
/// </summary>
/// <param name="Status">The status the responder reports for the certificate.</param>
/// <param name="UncheckedResponder">
/// The encoded certificate of a delegated responder whose own revocation the caller still has to check, or an empty
/// value when the issuer signed the response or the responder carries the <c>id-pkix-ocsp-nocheck</c> extension.
/// </param>
internal sealed record OcspVerifiedStatus(
    ClientCertificateRevocationStatus Status,
    ReadOnlyMemory<byte> UncheckedResponder
);
