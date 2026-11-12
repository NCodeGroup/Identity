// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.AspNetCore.ProxyClientCertificate;

/// <summary>
/// Carries the reverse proxy's certificate that <see cref="ProxyClientCertificateMiddleware"/> verified.
/// </summary>
internal sealed class ProxyClientCertificateFeature(X509Certificate2 certificate)
    : IProxyClientCertificateFeature
{
    /// <inheritdoc />
    public X509Certificate2 Certificate { get; } = certificate;
}
