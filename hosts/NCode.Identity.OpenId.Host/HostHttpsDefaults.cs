// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Security;
using System.Security.Authentication;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace NCode.Identity.OpenId.Host;

/// <summary>
/// Provides the HTTPS defaults of every endpoint the host serves over TLS itself. The handshake accepts only TLS 1.2
/// and 1.3 (FAPI 1.0 Part 2 §8.5, RFC 8996), whatever the operating system enables, and only AEAD cipher suites with
/// forward secrecy where the platform lets a process choose them. The cipher suite policy needs .NET's
/// <see cref="CipherSuitesPolicy"/>, which Linux and macOS support and Windows (SChannel) does not.
/// </summary>
internal static class HostHttpsDefaults
{
    /// <summary>
    /// Gets the cipher suites a handshake may negotiate where the platform lets a process choose them.
    /// </summary>
    internal static IReadOnlyList<TlsCipherSuite> PermittedCipherSuites { get; } =
    [
        TlsCipherSuite.TLS_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
    ];

    /// <summary>
    /// Configures the HTTPS defaults of the host's endpoints.
    /// </summary>
    /// <param name="https">The <see cref="HttpsConnectionAdapterOptions"/> to configure.</param>
    internal static void Configure(HttpsConnectionAdapterOptions https)
    {
        https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        https.AllowAnyClientCertificate();
        https.OnAuthenticate = OnAuthenticate;
    }

    private static void OnAuthenticate(
        ConnectionContext context,
        SslServerAuthenticationOptions sslOptions
    )
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        sslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(PermittedCipherSuites);
    }
}
