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
/// Provides the ability to determine whether an issuer has revoked a certificate. Every network retrieval goes through
/// the server's outbound-request guard, so a revocation source can never be used to reach a private address. A host
/// replaces it to add another source of revocation information, for example OCSP.
/// </summary>
/// <remarks>
/// An implementation fails closed: when it cannot determine the status, it returns
/// <see cref="ClientCertificateRevocationStatus.Unknown"/>, which the caller treats as not trusted.
/// </remarks>
[PublicAPI]
public interface IClientCertificateRevocationChecker
{
    /// <summary>
    /// Determines whether <paramref name="issuer"/> has revoked <paramref name="certificate"/>.
    /// </summary>
    /// <param name="certificate">The <see cref="X509Certificate2"/> to check.</param>
    /// <param name="issuer">The <see cref="X509Certificate2"/> that issued <paramref name="certificate"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ClientCertificateRevocationStatus"/> of <paramref name="certificate"/>.</returns>
    ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        CancellationToken cancellationToken
    );
}
