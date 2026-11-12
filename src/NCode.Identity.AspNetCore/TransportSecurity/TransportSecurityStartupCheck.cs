// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace NCode.Identity.AspNetCore.TransportSecurity;

/// <summary>
/// Fails startup in the Production environment unless every endpoint Kestrel bound is HTTPS and the certificate each
/// one serves is usable: it completes a TLS handshake, is inside its validity period, has a strong key and signature
/// algorithm and is not the ASP.NET Core development certificate.
/// </summary>
/// <remarks>
/// Endpoints from configuration, from <c>ASPNETCORE_URLS</c> and from code exist only once Kestrel has bound them, so
/// the check runs at <see cref="StartedAsync"/>, when <see cref="IServerAddressesFeature.Addresses"/> lists each one
/// with its real scheme.
/// </remarks>
// See ADR-1504.
internal sealed class TransportSecurityStartupCheck(
    IServer server,
    TransportCertificateProbe probe,
    TimeProvider timeProvider
) : IHostedLifecycleService
{
    /// <summary>
    /// The message of the failure when the server is not Kestrel.
    /// </summary>
    internal const string NotKestrelMessage =
        "Refusing to start in the Production environment: the server is not Kestrel, so the host cannot verify that it serves HTTPS with a usable certificate. Run the host on Kestrel.";

    /// <summary>
    /// Creates the message of the failure that lists every transport security problem.
    /// </summary>
    /// <param name="failures">The problems that were found.</param>
    internal static string CreateFailureMessage(IReadOnlyList<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var lines = new List<string>
        {
            $"Refusing to start in the Production environment: {failures.Count} transport security problem(s). The host serves HTTPS only, with a certificate that is valid, has a private key and is not the development certificate.",
        };
        lines.AddRange(failures.Select(failure => $"  - {failure}"));
        lines.Add(
            "Bind HTTPS endpoints only (for example ASPNETCORE_URLS=https://+:8443) and configure the certificate through Kestrel:Certificates:Default or Kestrel:Endpoints:<name>:Certificate."
        );
        return string.Join(Environment.NewLine, lines);
    }

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        // Kestrel's server type is internal to the assembly that holds KestrelServer, so Kestrel is recognized by assembly.
        if (server.GetType().Assembly != typeof(KestrelServer).Assembly)
        {
            throw new InvalidOperationException(NotKestrelMessage);
        }

        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        var inspection = TransportEndpointInspector.Inspect(addresses);

        var now = timeProvider.GetUtcNow();
        var failures = new List<string>(inspection.Failures);
        foreach (var endpoint in inspection.TlsEndpoints)
        {
            var result = await probe.ReadServedCertificateAsync(endpoint, cancellationToken);
            using var certificate = result.Certificate;
            if (certificate is null)
            {
                failures.Add(
                    $"The endpoint '{endpoint}' completed no TLS handshake ({result.FailureReason}), so its certificate is missing, has no private key or cannot be used."
                );
                continue;
            }

            foreach (var failure in TransportCertificateInspector.FindFailures(certificate, now))
            {
                failures.Add($"The certificate served at '{endpoint}' {failure}");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(CreateFailureMessage(failures));
        }
    }

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
