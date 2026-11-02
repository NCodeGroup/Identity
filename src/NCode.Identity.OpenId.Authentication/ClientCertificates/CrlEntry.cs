// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// An entry of a certificate revocation list (RFC 5280 §5.3).
/// </summary>
/// <param name="SerialNumber">The serial number of the certificate the entry lists.</param>
/// <param name="CertificateIssuer">
/// The name of the authority that issued the listed certificate: the certificate issuer extension of the entry or of
/// an earlier entry, otherwise the issuer of the list.
/// </param>
/// <param name="IsRemoveFromCrl">Whether the reason of the entry is <c>removeFromCRL</c>, which lifts a hold.</param>
internal sealed record CrlEntry(
    BigInteger SerialNumber,
    X500DistinguishedName CertificateIssuer,
    bool IsRemoveFromCrl
);
