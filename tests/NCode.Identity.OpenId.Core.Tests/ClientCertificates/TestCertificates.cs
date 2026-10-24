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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

/// <summary>
/// Creates the certificates the client-certificate tests present: certificate authorities, the client certificates
/// they issue, and self-signed certificates. Every key is an ECDSA P-256 key, which is fast to generate.
/// </summary>
internal static class TestCertificates
{
    public const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private const string OcspSigningOid = "1.3.6.1.5.5.7.3.9";
    private const string OcspNoCheckOid = "1.3.6.1.5.5.7.48.1.5";

    public static X509Certificate2 CreateRootAuthority(string commonName)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        AddAuthorityExtensions(request);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );
    }

    public static X509Certificate2 CreateIntermediateAuthority(
        X509Certificate2 issuer,
        string commonName,
        IReadOnlyList<string>? crlDistributionPoints = null
    )
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        AddAuthorityExtensions(request);
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false)
        );
        AddCrlDistributionPoints(request, crlDistributionPoints);
        using var certificate = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddMonths(6),
            CreateSerialNumber()
        );
        return certificate.CopyWithPrivateKey(key);
    }

    public static X509Certificate2 CreateCrlSigner(
        X509Certificate2 issuer,
        string commonName,
        IReadOnlyList<string>? crlDistributionPoints = null
    ) =>
        CreateSigner(
            issuer,
            commonName,
            X509KeyUsageFlags.CrlSign,
            extendedKeyUsage: false,
            noCheck: false,
            crlDistributionPoints
        );

    public static X509Certificate2 CreateOcspResponder(
        X509Certificate2 issuer,
        string commonName,
        bool noCheck,
        IReadOnlyList<string>? crlDistributionPoints = null
    ) =>
        CreateSigner(
            issuer,
            commonName,
            X509KeyUsageFlags.DigitalSignature,
            extendedKeyUsage: true,
            noCheck,
            crlDistributionPoints
        );

    public static X509Certificate2 CreateClientCertificate(
        X509Certificate2 issuer,
        string subjectName,
        ClientCertificateOptions? options = null
    )
    {
        var effective = options ?? new ClientCertificateOptions();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true)
        );
        if (effective.ExtendedKeyUsages is { } usages)
        {
            var oids = new OidCollection();
            foreach (var usage in usages)
                oids.Add(new Oid(usage));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(oids, false));
        }

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        );
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false)
        );
        AddCrlDistributionPoints(request, effective.CrlDistributionPoints);
        if (effective.OcspUris is { Count: > 0 } ocspUris)
        {
            request.CertificateExtensions.Add(
                new X509AuthorityInformationAccessExtension(ocspUris, null)
            );
        }

        foreach (var extension in effective.ExtraExtensions)
            request.CertificateExtensions.Add(extension);

        using var certificate = request.Create(
            issuer,
            effective.NotBefore ?? DateTimeOffset.UtcNow.AddHours(-1),
            effective.NotAfter ?? DateTimeOffset.UtcNow.AddDays(30),
            effective.SerialNumber ?? CreateSerialNumber()
        );
        return certificate.CopyWithPrivateKey(key);
    }

    public static X509Certificate2 CreateSelfSigned(string subjectName)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new(ClientAuthenticationOid) },
                false
            )
        );
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );
    }

    public static string ToBase64Der(X509Certificate2 certificate) =>
        Convert.ToBase64String(certificate.RawData);

    public static IReadOnlyList<string> ServerAuthenticationOnly { get; } =
    [ServerAuthenticationOid];

    public static byte[] CreateSerialNumber()
    {
        var serialNumber = new byte[8];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;
        return serialNumber;
    }

    private static X509Certificate2 CreateSigner(
        X509Certificate2 issuer,
        string commonName,
        X509KeyUsageFlags keyUsage,
        bool extendedKeyUsage,
        bool noCheck,
        IReadOnlyList<string>? crlDistributionPoints
    )
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
        if (extendedKeyUsage)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new(OcspSigningOid) }, false)
            );
        }

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        );
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false)
        );
        if (noCheck)
            request.CertificateExtensions.Add(
                new X509Extension(OcspNoCheckOid, [0x05, 0x00], false)
            );

        AddCrlDistributionPoints(request, crlDistributionPoints);
        using var certificate = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddMonths(6),
            CreateSerialNumber()
        );
        return certificate.CopyWithPrivateKey(key);
    }

    private static void AddCrlDistributionPoints(
        CertificateRequest request,
        IReadOnlyList<string>? crlDistributionPoints
    )
    {
        if (crlDistributionPoints is not { Count: > 0 })
            return;

        request.CertificateExtensions.Add(
            CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(
                crlDistributionPoints
            )
        );
    }

    private static void AddAuthorityExtensions(CertificateRequest request)
    {
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true
            )
        );
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        );
    }
}
