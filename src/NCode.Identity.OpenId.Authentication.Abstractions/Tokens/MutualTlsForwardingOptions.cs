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

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Configures how the default <see cref="IClientCertificateResolver"/> reads the mutual-TLS client certificate when
/// TLS terminates at a reverse proxy, load balancer, or service mesh rather than on the application's own connection.
/// Bound from the <c>OpenId:MutualTls</c> configuration section.
/// </summary>
/// <remarks>
/// Only set <see cref="CertificateHeaderName"/> when the application is reachable exclusively through the trusted proxy
/// that sets the header (or the proxy-to-app hop is itself authenticated); otherwise a client could spoof the header
/// and bind a token to an arbitrary certificate. When unset, the resolver reads the certificate negotiated directly on
/// the connection (direct-TLS deployments).
/// </remarks>
[PublicAPI]
public sealed class MutualTlsForwardingOptions
{
    /// <summary>
    /// The configuration section (<c>OpenId:MutualTls</c>) bound to <see cref="MutualTlsForwardingOptions"/>.
    /// </summary>
    public const string ConfigurationSectionKey = "OpenId:MutualTls";

    /// <summary>
    /// Gets or sets the name of the trusted header that carries the forwarded client certificate (for example
    /// <c>X-Forwarded-Client-Cert</c>, <c>X-Amzn-Mtls-Clientcert</c>, or <c>ssl-client-cert</c>). When unset,
    /// forwarding is disabled and the connection certificate is used.
    /// </summary>
    public string? CertificateHeaderName { get; set; }

    /// <summary>
    /// Gets or sets how the forwarded certificate is encoded in the header. Defaults to
    /// <see cref="MutualTlsClientCertificateFormat.Base64Der"/>.
    /// </summary>
    public MutualTlsClientCertificateFormat Format { get; set; } =
        MutualTlsClientCertificateFormat.Base64Der;
}
