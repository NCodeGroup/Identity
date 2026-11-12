// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.AspNetCore.Options;

/// <summary>
/// Configures the requirement that the reverse proxy in front of the host authenticates to it with a client
/// certificate on the TLS connection. Bound from the <c>ProxyClientCertificate</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// While <see cref="Required"/> is <c>true</c>, the certificate on the connection is the proxy's, never an end
/// client's. A deployment that serves end clients' certificates on the connection itself does not enable it.
/// </para>
/// <para>
/// The host must be reachable only through the proxy: a request that does not pass through the proxy carries no
/// certificate and is refused, except the liveness and readiness probes.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class ProxyClientCertificateOptions
{
    /// <summary>
    /// The configuration section (<c>ProxyClientCertificate</c>) bound to <see cref="ProxyClientCertificateOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "ProxyClientCertificate";

    /// <summary>
    /// Gets or sets a value indicating whether the host refuses a request whose connection carries no certificate that
    /// the proxy is expected to present. Defaults to <c>false</c>.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the path of the PEM file that holds the certificate authorities that must have issued the proxy's
    /// certificate. The file is read again when its last-write time changes. Required while <see cref="Required"/> is
    /// <c>true</c>.
    /// </summary>
    public string? CertificateAuthorityPath { get; set; }

    /// <summary>
    /// Gets or sets the subject distinguished names, such as <c>CN=ingress</c>, of which the proxy's certificate has
    /// one. At least one is required while <see cref="Required"/> is <c>true</c>. Each entry is compared with the
    /// certificate's subject by exact match of the distinguished name's canonical text, so case, attribute order and
    /// string encoding must match. Empty by default.
    /// </summary>
    public string[] AllowedSubjects { get; set; } = [];
}
