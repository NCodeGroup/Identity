// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.AspNetCore.TransportSecurity;

/// <summary>
/// The outcome of probing an HTTPS endpoint for the certificate it serves.
/// </summary>
/// <param name="Certificate">The certificate the endpoint serves, or <see langword="null"/> when no handshake completed. The caller owns the certificate.</param>
/// <param name="FailureReason">Why no handshake completed, or <see langword="null"/> when the endpoint served a certificate.</param>
internal sealed record TransportCertificateProbeResult(
    X509Certificate2? Certificate,
    string? FailureReason
)
{
    /// <summary>
    /// Creates the result of a handshake that completed and served a certificate.
    /// </summary>
    /// <param name="certificate">The certificate the endpoint serves.</param>
    internal static TransportCertificateProbeResult Success(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new TransportCertificateProbeResult(certificate, null);
    }

    /// <summary>
    /// Creates the result of an endpoint on which no handshake completed.
    /// </summary>
    /// <param name="failureReason">Why no handshake completed.</param>
    internal static TransportCertificateProbeResult Failure(string failureReason)
    {
        ArgumentException.ThrowIfNullOrEmpty(failureReason);

        return new TransportCertificateProbeResult(null, failureReason);
    }
}
