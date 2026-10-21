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
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.Server;

/// <summary>
/// Lets a server that terminates TLS itself receive the client certificates of mutual-TLS clients (RFC 8705): when an
/// HTTPS endpoint requests a client certificate — <c>Kestrel:EndpointDefaults:ClientCertificateMode</c> or an
/// endpoint's own <c>ClientCertificateMode</c> — the TLS handshake accepts whatever certificate the client presents,
/// and the server decides what it proves.
/// </summary>
/// <remarks>
/// Kestrel's default validation refuses, during the handshake, any certificate that does not chain to the machine's
/// trust store, which would refuse every <c>self_signed_tls_client_auth</c> certificate and every certificate issued by
/// a tenant's own PKI. The certificate is instead validated where its meaning is known: a client-authentication handler
/// matches it to the client's registered key or validates its chain against the configured trust anchors, and a
/// certificate-bound access token is bound to its thumbprint. Safe to leave wired: an endpoint that requests no client
/// certificate (the default) never receives one, and a server behind a proxy reads the forwarded certificate instead.
/// </remarks>
// See ADR-0172 and ADR-0202.
[PublicAPI]
public static class DirectClientCertificateRegistration
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Configures every Kestrel HTTPS endpoint to accept, at the TLS layer, any client certificate it requests, so
        /// the server validates the certificate against the client it authenticates.
        /// </summary>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddDirectClientCertificates()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            serviceCollection.Configure<KestrelServerOptions>(kestrelOptions =>
                kestrelOptions.ConfigureHttpsDefaults(ConfigureHttpsDefaults)
            );

            return serviceCollection;
        }
    }

    internal static void ConfigureHttpsDefaults(HttpsConnectionAdapterOptions httpsOptions) =>
        httpsOptions.AllowAnyClientCertificate();
}
