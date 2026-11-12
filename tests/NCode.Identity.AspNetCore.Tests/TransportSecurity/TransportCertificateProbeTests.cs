// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.AspNetCore.TransportSecurity;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.TransportSecurity;

public sealed class TransportCertificateProbeTests
{
    private static DateTimeOffset Now { get; } = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    #region ReadServedCertificateAsync Tests

    [Fact]
    public async Task ReadServedCertificateAsync_WhenTheEndpointServesTls_ReturnsTheServedCertificate()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(Now);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = ServeTlsAsync(listener, certificate, CancellationToken.None);
            var endpoint = new Uri($"https://127.0.0.1:{port}");

            using var served = await new TransportCertificateProbe().ReadServedCertificateAsync(
                endpoint,
                CancellationToken.None
            );

            Assert.NotNull(served);
            Assert.Equal(certificate.Thumbprint, served.Thumbprint);
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ReadServedCertificateAsync_WhenNothingListens_ReturnsNull()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var endpoint = new Uri($"https://127.0.0.1:{port}");

        var served = await new TransportCertificateProbe().ReadServedCertificateAsync(
            endpoint,
            CancellationToken.None
        );

        Assert.Null(served);
    }

    [Fact]
    public async Task ReadServedCertificateAsync_WhenTheServerClosesTheConnection_ReturnsNull()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = CloseConnectionAsync(listener, CancellationToken.None);
            var endpoint = new Uri($"https://127.0.0.1:{port}");

            var served = await new TransportCertificateProbe().ReadServedCertificateAsync(
                endpoint,
                CancellationToken.None
            );

            Assert.Null(served);
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ReadServedCertificateAsync_WhenTheServerRequiresAClientCertificate_ReturnsTheServedCertificate()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(Now);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = ServeTlsRequiringClientCertificateAsync(
                listener,
                certificate,
                CancellationToken.None
            );
            var endpoint = new Uri($"https://127.0.0.1:{port}");

            using var served = await new TransportCertificateProbe().ReadServedCertificateAsync(
                endpoint,
                CancellationToken.None
            );

            Assert.NotNull(served);
            Assert.Equal(certificate.Thumbprint, served.Thumbprint);
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ReadServedCertificateAsync_WhenTheCallerCancels_Throws()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var serverCancellation = new CancellationTokenSource();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = HoldConnectionAsync(listener, serverCancellation.Token);
            var endpoint = new Uri($"https://127.0.0.1:{port}");
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new TransportCertificateProbe()
                    .ReadServedCertificateAsync(endpoint, cancellation.Token)
                    .AsTask()
            );

            await serverCancellation.CancelAsync();
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }

    #endregion

    #region GetCandidateHosts Tests

    [Fact]
    public void GetCandidateHosts_WhenTheHostIsTheIPv6Wildcard_ReturnsIPv6ThenIPv4Loopback()
    {
        var hosts = TransportCertificateProbe.GetCandidateHosts(new Uri("https://[::]:8443"));

        Assert.Equal([IPAddress.IPv6Loopback.ToString(), IPAddress.Loopback.ToString()], hosts);
    }

    [Fact]
    public void GetCandidateHosts_WhenTheHostIsTheIPv4Wildcard_ReturnsIPv4Loopback()
    {
        var hosts = TransportCertificateProbe.GetCandidateHosts(new Uri("https://0.0.0.0:8443"));

        Assert.Equal([IPAddress.Loopback.ToString()], hosts);
    }

    [Fact]
    public void GetCandidateHosts_WhenTheHostIsAName_ReturnsTheName()
    {
        var hosts = TransportCertificateProbe.GetCandidateHosts(new Uri("https://localhost:8443"));

        Assert.Equal(["localhost"], hosts);
    }

    #endregion

    private static async ValueTask ServeTlsAsync(
        TcpListener listener,
        X509Certificate2 certificate,
        CancellationToken cancellationToken
    )
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var ssl = new SslStream(client.GetStream());
        await ssl.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions { ServerCertificate = certificate },
            cancellationToken
        );
    }

    private static async ValueTask ServeTlsRequiringClientCertificateAsync(
        TcpListener listener,
        X509Certificate2 certificate,
        CancellationToken cancellationToken
    )
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var ssl = new SslStream(client.GetStream());
        try
        {
            await ssl.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    ClientCertificateRequired = true,
                },
                cancellationToken
            );
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            // The server rejects the missing client certificate after the client's handshake completed.
        }
    }

    private static async ValueTask HoldConnectionAsync(
        TcpListener listener,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The test ends the held connection by cancelling.
        }
    }

    private static async ValueTask CloseConnectionAsync(
        TcpListener listener,
        CancellationToken cancellationToken
    )
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
    }
}
