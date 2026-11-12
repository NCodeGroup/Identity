// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.AspNetCore.Tests.ProxyClientCertificate;

/// <summary>
/// Creates the certificate authorities and the leaf certificates for the proxy client certificate tests.
/// </summary>
internal static class ProxyClientCertificateTestCertificates
{
    internal const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    internal const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// A self-signed certificate authority with the given subject, valid from a year before <paramref name="now"/>
    /// for two years.
    /// </summary>
    internal static X509Certificate2 CreateCertificateAuthority(string subject, DateTimeOffset now)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true
            )
        );
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, critical: true)
        );
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false)
        );

        return request.CreateSelfSigned(now.AddYears(-1), now.AddYears(1));
    }

    /// <summary>
    /// A leaf certificate signed by <paramref name="issuer"/> with the given subject, extended key usages and validity
    /// period. A <c>null</c> <paramref name="usages"/> adds no extended key usage extension.
    /// </summary>
    internal static X509Certificate2 CreateLeaf(
        X509Certificate2 issuer,
        string subject,
        string[]? usages,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter
    )
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
        AddLeafExtensions(request, usages);
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                issuer,
                includeKeyIdentifier: true,
                includeIssuerAndSerial: false
            )
        );

        var serialNumber = new byte[8];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;
        serialNumber[0] |= 0x01;

        return request.Create(issuer, notBefore, notAfter, serialNumber);
    }

    /// <summary>
    /// A self-signed leaf certificate with the given subject and the client authentication extended key usage, valid
    /// from a day before <paramref name="now"/> for 30 days.
    /// </summary>
    internal static X509Certificate2 CreateSelfSignedLeaf(string subject, DateTimeOffset now)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
        AddLeafExtensions(request, [ClientAuthenticationOid]);
        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(30));
    }

    private static void AddLeafExtensions(CertificateRequest request, string[]? usages)
    {
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true)
        );

        if (usages is not null)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    [.. usages.Select(usage => new Oid(usage))],
                    critical: false
                )
            );
        }
    }
}
