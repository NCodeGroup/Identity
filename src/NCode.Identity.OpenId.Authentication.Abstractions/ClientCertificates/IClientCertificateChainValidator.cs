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
using NCode.Identity.OpenId.Authentication.Clients;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the ability to decide whether the mutual-TLS client certificate a client presented is issued by a PKI the
/// client trusts, which is what makes the certificate's subject a credential for <c>tls_client_auth</c>. The default
/// builds the chain to the client's effective <c>tls_client_auth_trust_anchors</c>, treating any forwarded
/// intermediates as untrusted hints, and requires the client-authentication extended key usage, a validity period that
/// covers the current time, and the revocation status demanded by <c>tls_client_auth_revocation_mode</c>. Replace it
/// only to validate against a bespoke trust source.
/// </summary>
/// <remarks>
/// The validation never trusts the platform's certificate store, and an implementation fails closed: a client with no
/// trust anchors trusts no certificate.
/// </remarks>
[PublicAPI]
public interface IClientCertificateChainValidator
{
    /// <summary>
    /// Determines whether <paramref name="presentedCertificate"/> chains to a trust anchor of
    /// <paramref name="openIdClient"/> and is valid for client authentication now.
    /// </summary>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> whose settings carry the trust anchors.</param>
    /// <param name="presentedCertificate">The <see cref="PresentedClientCertificate"/> to validate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> when the certificate is trusted for client authentication; otherwise,
    /// <see langword="false"/>.</returns>
    ValueTask<bool> ValidateAsync(
        OpenIdClient openIdClient,
        PresentedClientCertificate presentedCertificate,
        CancellationToken cancellationToken
    );
}
