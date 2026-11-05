// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// A self-signed TLS certificate for one test run, which covers every host name that the end-to-end suites use.
/// </summary>
public sealed class TestCertificate : IDisposable
{
    private TestCertificate(
        X509Certificate2 certificate,
        string certificatePem,
        string privateKeyPem
    )
    {
        Certificate = certificate;
        CertificatePem = certificatePem;
        PrivateKeyPem = privateKeyPem;
    }

    /// <summary>
    /// Gets the certificate with a persisted private key, which Windows SChannel requires to serve it.
    /// </summary>
    public X509Certificate2 Certificate { get; }

    /// <summary>
    /// Gets the PEM encoding of the certificate.
    /// </summary>
    public string CertificatePem { get; }

    /// <summary>
    /// Gets the PEM encoding of the PKCS#8 private key.
    /// </summary>
    public string PrivateKeyPem { get; }

    /// <summary>
    /// Creates a new self-signed certificate for <c>localhost</c>, <c>host.testcontainers.internal</c>,
    /// <c>127.0.0.1</c> and <c>::1</c>.
    /// </summary>
    public static TestCertificate Create()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true
            )
        );
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication")],
                false
            )
        );

        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName("localhost");
        alternativeNames.AddDnsName("host.testcontainers.internal");
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        alternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(alternativeNames.Build());

        var now = TimeProvider.System.GetUtcNow();
        using var ephemeral = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));

        // Windows SChannel cannot serve a certificate whose private key is ephemeral.
        var certificate = X509CertificateLoader.LoadPkcs12(
            ephemeral.Export(X509ContentType.Pkcs12),
            null
        );

        return new TestCertificate(
            certificate,
            ephemeral.ExportCertificatePem(),
            rsa.ExportPkcs8PrivateKeyPem()
        );
    }

    /// <summary>
    /// Determines whether the presented certificate is exactly this certificate.
    /// </summary>
    public bool IsTrusted(X509Certificate? presented)
    {
        return presented is not null
            && presented.GetRawCertData().AsSpan().SequenceEqual(Certificate.RawData);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Certificate.Dispose();
    }
}
