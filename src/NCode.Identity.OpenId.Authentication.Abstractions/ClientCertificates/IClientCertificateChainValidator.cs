// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
