#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
/// Configures how the default <see cref="IClientCertificateResolver"/> reads the mutual-TLS client certificate when
/// TLS terminates at a reverse proxy, load balancer, or service mesh rather than on the application's own connection.
/// Bound from the <c>MutualTls</c> subsection of the OpenID configuration section (<c>OpenId:MutualTls</c> by default).
/// </summary>
/// <remarks>
/// <para>
/// Only set <see cref="CertificateHeaderName"/> when the application is reachable exclusively through the trusted proxy
/// that sets the header and strips any value a client sent (or the proxy-to-app hop is itself authenticated); otherwise
/// a client could spoof the header and present an arbitrary certificate.
/// </para>
/// <para>
/// When the header is configured it is the only certificate source: a request without it presented no certificate,
/// and the certificate negotiated on the connection — at most the proxy's own — is never read. A header whose value the
/// configured <see cref="Format"/> cannot decode fails the request with <c>invalid_request</c> rather than reading as
/// no certificate. When the header is unset, the resolver reads the certificate negotiated directly on the connection
/// (direct-TLS deployments).
/// </para>
/// </remarks>
[PublicAPI]
public sealed class MutualTlsForwardingOptions
{
    /// <summary>
    /// Gets or sets the name of the trusted header that carries the forwarded client certificate (for example
    /// <c>X-Forwarded-Tls-Client-Cert</c>, <c>X-Forwarded-Client-Cert</c>, <c>X-Amzn-Mtls-Clientcert</c>, or
    /// <c>ssl-client-cert</c>). When unset, forwarding is disabled and the connection certificate is used.
    /// </summary>
    public string? CertificateHeaderName { get; set; }

    /// <summary>
    /// Gets or sets the name of the format in which the header carries the certificate: the
    /// <see cref="IClientCertificateHeaderDecoder.Format"/> of a registered decoder (the known names are the values of
    /// <see cref="ClientCertificateHeaderFormats"/>). A name no registered decoder reads stops the host when it starts.
    /// Defaults to <see cref="ClientCertificateHeaderFormats.Base64Der"/>.
    /// </summary>
    public string Format { get; set; } = ClientCertificateHeaderFormats.Base64Der;
}
