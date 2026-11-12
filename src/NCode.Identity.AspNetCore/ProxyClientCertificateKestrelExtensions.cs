// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the Kestrel counterpart to <see cref="ProxyClientCertificateRegistration"/>: makes an HTTPS endpoint
/// request the reverse proxy's client certificate.
/// </summary>
[PublicAPI]
public static class ProxyClientCertificateKestrelExtensions
{
    extension(HttpsConnectionAdapterOptions https)
    {
        /// <summary>
        /// When <see cref="ProxyClientCertificateOptions.Required"/> is true, makes the endpoint request a client
        /// certificate without requiring one and accept any certificate at the handshake. Does nothing otherwise.
        /// </summary>
        /// <remarks>
        /// The handshake accepts any certificate, or none, so that the kubelet's probes and the host's own loopback
        /// connection succeed; the middleware added by <c>UseProxyClientCertificate</c> decides whether the
        /// certificate is the proxy's.
        /// </remarks>
        /// <param name="options">The <see cref="ProxyClientCertificateOptions"/> that decide whether to request it.</param>
        public void RequestProxyClientCertificate(ProxyClientCertificateOptions options)
        {
            ArgumentNullException.ThrowIfNull(https);
            ArgumentNullException.ThrowIfNull(options);

            if (!options.Required)
            {
                return;
            }

            https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
            https.AllowAnyClientCertificate();
        }
    }
}
