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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Describes one revocation check: the certificate, the certificate that issued it, and what a source needs to trust a
/// signer of revocation information other than the issuer, which is a path to the same trust anchor.
/// </summary>
[PublicAPI]
public sealed class ClientCertificateRevocationContext
{
    /// <summary>
    /// Gets the <see cref="X509Certificate2"/> whose status is checked.
    /// </summary>
    public required X509Certificate2 Certificate { get; init; }

    /// <summary>
    /// Gets the <see cref="X509Certificate2"/> that issued <see cref="Certificate"/>, the next element of the validated
    /// chain.
    /// </summary>
    public required X509Certificate2 Issuer { get; init; }

    /// <summary>
    /// Gets the <see cref="X509Certificate2"/> the validated chain ends in. A signer of revocation information other
    /// than the issuer is trusted only through a path to it.
    /// </summary>
    public required X509Certificate2 TrustAnchor { get; init; }

    /// <summary>
    /// Gets the other certificates that may complete such a path: the configured intermediates and the ones the client
    /// presented. None of them is trusted on its own.
    /// </summary>
    public IReadOnlyCollection<X509Certificate2> Intermediates { get; init; } = [];

    /// <summary>
    /// Gets how many checks enclose this one: zero for a certificate of the presented chain, and one more for each
    /// certificate checked to trust a signer.
    /// </summary>
    public int Depth { get; init; }
}
