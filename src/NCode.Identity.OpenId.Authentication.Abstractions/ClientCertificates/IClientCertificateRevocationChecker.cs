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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the ability to determine whether an issuer has revoked a certificate. The default implementation asks each
/// registered <see cref="IClientCertificateRevocationSource"/> in registration order (OCSP, then certificate
/// revocation lists) and returns the first <see cref="ClientCertificateRevocationStatus.Good"/> or
/// <see cref="ClientCertificateRevocationStatus.Revoked"/>. Every network retrieval goes through the server's
/// outbound-request guard, so a revocation source can never be used to reach a private address. A host adds a source
/// by registering an <see cref="IClientCertificateRevocationSource"/>, and replaces the checker only to combine
/// sources differently.
/// </summary>
/// <remarks>
/// An implementation fails closed: when it cannot determine the status, it returns
/// <see cref="ClientCertificateRevocationStatus.Unknown"/>, which the caller treats as not trusted.
/// </remarks>
[PublicAPI]
public interface IClientCertificateRevocationChecker
{
    /// <summary>
    /// Determines whether the issuer has revoked the certificate that <paramref name="context"/> describes.
    /// </summary>
    /// <param name="context">The <see cref="ClientCertificateRevocationContext"/> with the certificate, its issuer, and the path context of the check.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ClientCertificateRevocationStatus"/> of the certificate.</returns>
    ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    );
}
