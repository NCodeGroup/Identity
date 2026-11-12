// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.AspNetCore.TransportSecurity;

namespace NCode.Identity.AspNetCore.Tests.TransportSecurity;

/// <summary>
/// Creates self-signed certificates for the transport security tests. Each certificate that carries a private key
/// went through a PFX round-trip, because on Windows Kestrel and <see cref="System.Net.Security.SslStream"/> cannot
/// use the key of a freshly created certificate.
/// </summary>
internal static class TransportSecurityTestCertificates
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// A certificate that passes every rule: ECDSA P-256, SHA-256, valid from a day before <paramref name="now"/> for
    /// 90 days.
    /// </summary>
    internal static X509Certificate2 CreateValid(DateTimeOffset now) =>
        CreateWithValidity(now.AddDays(-1), now.AddDays(90));

    /// <summary>
    /// An ECDSA P-256 certificate with SHA-256 that is valid for the given period.
    /// </summary>
    internal static X509Certificate2 CreateWithValidity(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter
    ) => Create(notBefore, notAfter, extension: null);

    /// <summary>
    /// An RSA certificate with the given key size and signature hash, valid from a day before <paramref name="now"/>
    /// for 90 days.
    /// </summary>
    internal static X509Certificate2 CreateRsa(
        int keySizeInBits,
        HashAlgorithmName signatureHash,
        DateTimeOffset now
    )
    {
        using var rsa = RSA.Create(keySizeInBits);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            signatureHash,
            RSASignaturePadding.Pkcs1
        );
        var notBefore = now.AddDays(-1);
        var notAfter = now.AddDays(90);

        // The framework's signature generator does not sign with SHA-1, so that certificate uses a generator of this
        // file's own.
        return signatureHash == HashAlgorithmName.SHA1
            ? FinishSha1(request, rsa, notBefore, notAfter)
            : Finish(request, notBefore, notAfter);
    }

    /// <summary>
    /// A valid certificate that carries the extension that marks the ASP.NET Core HTTPS development certificate.
    /// </summary>
    internal static X509Certificate2 CreateDevelopmentMarked(DateTimeOffset now) =>
        Create(
            now.AddDays(-1),
            now.AddDays(90),
            new X509Extension(
                new Oid(TransportCertificateInspector.AspNetCoreDevelopmentCertificateOid),
                [1],
                critical: false
            )
        );

    /// <summary>
    /// A copy of the certificate that holds only its public part.
    /// </summary>
    internal static X509Certificate2 WithoutPrivateKey(X509Certificate2 certificate) =>
        X509CertificateLoader.LoadCertificate(certificate.RawData);

    private static X509Certificate2 Create(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        X509Extension? extension
    )
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", ecdsa, HashAlgorithmName.SHA256);
        if (extension is not null)
        {
            request.CertificateExtensions.Add(extension);
        }

        return Finish(request, notBefore, notAfter);
    }

    private static X509Certificate2 Finish(
        CertificateRequest request,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter
    )
    {
        AddServerExtensions(request);

        using var selfSigned = request.CreateSelfSigned(notBefore, notAfter);
        return Reload(selfSigned);
    }

    private static X509Certificate2 FinishSha1(
        CertificateRequest request,
        RSA rsa,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter
    )
    {
        AddServerExtensions(request);

        var subject = new X500DistinguishedName("CN=localhost");
        var serialNumber = new byte[8];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;
        serialNumber[0] |= 0x01;

        using var publicOnly = request.Create(
            subject,
            new Sha1RsaSignatureGenerator(rsa),
            notBefore,
            notAfter,
            serialNumber
        );
        using var selfSigned = publicOnly.CopyWithPrivateKey(rsa);
        return Reload(selfSigned);
    }

    private static void AddServerExtensions(CertificateRequest request)
    {
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid(ServerAuthenticationOid)], critical: false)
        );
    }

    private static X509Certificate2 Reload(X509Certificate2 certificate) =>
        X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), password: null);

    // Signs with RSA PKCS#1 and SHA-1 (sha1WithRSAEncryption), which the inspector must refuse.
    private sealed class Sha1RsaSignatureGenerator(RSA rsa) : X509SignatureGenerator
    {
        private static ReadOnlySpan<byte> AlgorithmIdentifier =>
            [
                0x30,
                0x0D,
                0x06,
                0x09,
                0x2A,
                0x86,
                0x48,
                0x86,
                0xF7,
                0x0D,
                0x01,
                0x01,
                0x05,
                0x05,
                0x00,
            ];

        protected override PublicKey BuildPublicKey() =>
            CreateForRSA(rsa, RSASignaturePadding.Pkcs1).PublicKey;

        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm) =>
            AlgorithmIdentifier.ToArray();

        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) =>
            rsa.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
    }
}
