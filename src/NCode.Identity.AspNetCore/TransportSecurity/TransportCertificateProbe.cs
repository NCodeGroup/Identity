// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.AspNetCore.TransportSecurity;

/// <summary>
/// Opens a loopback TLS connection to an HTTPS endpoint of this host and reads the certificate it serves. A
/// completed handshake proves that the host holds the certificate's private key.
/// </summary>
internal sealed class TransportCertificateProbe
{
    /// <summary>
    /// The longest a single connection attempt, handshake included, may take.
    /// </summary>
    internal static TimeSpan HandshakeTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Returns the loopback hosts to try for an endpoint, in order: a wildcard address maps to the loopback address
    /// of its family, and any other host is used as it is.
    /// </summary>
    internal static IReadOnlyList<string> GetCandidateHosts(Uri endpoint)
    {
        var host = endpoint.DnsSafeHost;
        if (!IPAddress.TryParse(host, out var address))
        {
            return [host];
        }

        if (address.Equals(IPAddress.IPv6Any))
        {
            return [IPAddress.IPv6Loopback.ToString(), IPAddress.Loopback.ToString()];
        }

        if (address.Equals(IPAddress.Any))
        {
            return [IPAddress.Loopback.ToString()];
        }

        return [host];
    }

    /// <summary>
    /// Connects to the endpoint, completes a TLS handshake and returns the certificate the endpoint serves, or
    /// <see langword="null"/> when no candidate host completes a handshake. The caller owns the returned certificate.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The startup check receives the probe as an injected instance."
    )]
    internal async ValueTask<X509Certificate2?> ReadServedCertificateAsync(
        Uri endpoint,
        CancellationToken cancellationToken
    )
    {
        foreach (var host in GetCandidateHosts(endpoint))
        {
            var served = await TryReadAsync(host, endpoint.Port, cancellationToken);
            if (served is not null)
            {
                return served;
            }
        }

        return null;
    }

    [SuppressMessage(
        "Security",
        "CA5359:Do Not Disable Certificate Validation",
        Justification = "The probe reads the certificate the host itself serves on loopback; it sends no data and trusts nothing."
    )]
    private static async ValueTask<X509Certificate2?> TryReadAsync(
        string host,
        int port,
        CancellationToken cancellationToken
    )
    {
        X509Certificate2? served = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(HandshakeTimeout);
            var token = timeout.Token;

            using var client = new TcpClient();
            await client.ConnectAsync(host, port, token);

            await using var ssl = new SslStream(
                client.GetStream(),
                leaveInnerStreamOpen: false,
                (_, certificate, _, _) =>
                {
                    if (certificate is not null)
                    {
                        served ??= X509CertificateLoader.LoadCertificate(
                            certificate.GetRawCertData()
                        );
                    }

                    return true;
                }
            );
            await ssl.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions { TargetHost = host },
                token
            );

            return served;
        }
        catch (Exception exception)
            when (exception
                    is AuthenticationException
                        or IOException
                        or SocketException
                        or OperationCanceledException
                && !cancellationToken.IsCancellationRequested
            )
        {
            served?.Dispose();
            return null;
        }
    }
}
