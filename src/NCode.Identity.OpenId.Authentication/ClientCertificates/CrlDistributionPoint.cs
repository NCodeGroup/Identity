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
