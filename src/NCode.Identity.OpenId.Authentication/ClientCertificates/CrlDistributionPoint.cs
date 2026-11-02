// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// A distribution point of a certificate that has at least one <c>http</c> or <c>https</c> location. <c>Reasons</c> is
/// <see langword="null"/> when the point covers every reason, and <c>CrlIssuer</c> is <see langword="null"/> when the
/// certificate's issuer publishes the list.
/// </summary>
/// <param name="Uris">The <c>http</c> and <c>https</c> locations of the point, as written.</param>
/// <param name="Reasons">The reasons the point covers as a mask, or <see langword="null"/> for every reason.</param>
/// <param name="CrlIssuer">The name of the authority that publishes the list, or <see langword="null"/>.</param>
internal sealed record CrlDistributionPoint(
    IReadOnlyList<string> Uris,
    int? Reasons,
    X500DistinguishedName? CrlIssuer
);
