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
