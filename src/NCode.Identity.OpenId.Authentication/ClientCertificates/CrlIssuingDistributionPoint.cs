// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// The issuing distribution point extension of a certificate revocation list (RFC 5280 §5.2.5), which declares the
/// scope of the list.
/// </summary>
/// <param name="Uris">The <c>http</c> and <c>https</c> locations of the distribution point, possibly none.</param>
/// <param name="HasName">Whether the extension names a distribution point.</param>
/// <param name="OnlyContainsUserCertificates">Whether the list covers only end-entity certificates.</param>
/// <param name="OnlyContainsAuthorityCertificates">Whether the list covers only authority certificates.</param>
/// <param name="OnlySomeReasons">The reasons the list covers as a mask, or <see langword="null"/> for every reason.</param>
/// <param name="IsIndirect">Whether the list may be issued by an authority other than the certificates' issuer.</param>
/// <param name="Encoded">The encoded extension value, which a delta list's scope is compared against.</param>
internal sealed record CrlIssuingDistributionPoint(
    IReadOnlyList<string> Uris,
    bool HasName,
    bool OnlyContainsUserCertificates,
    bool OnlyContainsAuthorityCertificates,
    int? OnlySomeReasons,
    bool IsIndirect,
    ReadOnlyMemory<byte> Encoded
);
