// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the ability to resolve the mutual-TLS client certificate presented for the current request, abstracting
/// over how TLS is terminated. The default reads the certificate negotiated on the connection when TLS terminates at
/// the application, and only the trusted forwarded header named by <see cref="MutualTlsForwardingOptions"/> when TLS
/// terminates at a reverse proxy, load balancer, or service mesh, decoding it with the
/// <see cref="IClientCertificateHeaderDecoder"/> registered for the configured format. Replace it only for a bespoke
/// certificate source.
/// </summary>
/// <remarks>
/// The resolved certificate is what the client presented, not a certificate the server trusts: a consumer that relies
/// on it for more than proof of possession validates it first (for example against a client's trust anchors).
/// </remarks>
[PublicAPI]
public interface IClientCertificateResolver
{
    /// <summary>
    /// Resolves the client certificate presented for the current request, or <see langword="null"/> when none was
    /// presented. A forwarded certificate that is present but cannot be decoded is an error, never an absent
    /// certificate.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="PresentedClientCertificate"/>, or <see langword="null"/> when none was presented.</returns>
    /// <exception cref="OpenIdException">Thrown with <c>invalid_request</c> when the trusted forwarded header
    /// carries a value the configured format cannot decode.</exception>
    ValueTask<PresentedClientCertificate?> ResolveCertificateAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    );
}
