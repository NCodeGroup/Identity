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
