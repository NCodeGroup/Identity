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
/// Provides one source of revocation information: OCSP, certificate revocation lists, or a source a package registers.
/// Sources are a strategy collection that the default <see cref="IClientCertificateRevocationChecker"/> asks in
/// registration order until one answers <see cref="ClientCertificateRevocationStatus.Good"/> or
/// <see cref="ClientCertificateRevocationStatus.Revoked"/>. A source answers
/// <see cref="ClientCertificateRevocationStatus.Unknown"/> when it has nothing to say, for example when the
/// certificate names no location or the data it retrieves is unusable. Every retrieval goes through the server's
/// outbound-request guard.
/// </summary>
[PublicAPI]
public interface IClientCertificateRevocationSource
{
    /// <summary>
    /// Gets the name of this source, compared ordinally. The names of the built-in sources are the values of
    /// <see cref="ClientCertificateRevocationSourceNames"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines whether the issuer has revoked the certificate that <paramref name="context"/> describes.
    /// </summary>
    /// <param name="context">The <see cref="ClientCertificateRevocationContext"/> that describes the check.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ClientCertificateRevocationStatus"/> of the certificate, or
    /// <see cref="ClientCertificateRevocationStatus.Unknown"/> when this source cannot determine it.</returns>
    ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    );
}
