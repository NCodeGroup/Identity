// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore.ProxyClientCertificate;

/// <summary>
/// Judges whether a certificate is the reverse proxy's: it chains to the certificate authorities in
/// <see cref="ProxyClientCertificateOptions.CertificateAuthorityPath"/>, has the client authentication extended key
/// usage, is inside its validity period and has one of <see cref="ProxyClientCertificateOptions.AllowedSubjects"/> as its
/// subject. Revocation is not checked. The certificate authorities are read again when the file's last-write time
/// changes.
/// </summary>
internal sealed class ProxyClientCertificateVerifier(
    IOptions<ProxyClientCertificateOptions> options,
    TimeProvider timeProvider
)
{
    /// <summary>
    /// The extended key usage OID for TLS client authentication.
    /// </summary>
    internal const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    private IOptions<ProxyClientCertificateOptions> Options { get; } = options;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private Lock Gate { get; } = new();
    private X509Certificate2Collection? Anchors { get; set; }
    private DateTime AnchorsLastWriteTimeUtc { get; set; }

    /// <summary>
    /// Returns whether the certificate is the proxy's.
    /// </summary>
    internal bool IsTrusted(X509Certificate2 certificate)
    {
        if (!HasClientAuthentication(certificate))
        {
            return false;
        }

        var settings = Options.Value;
        if (!IsAllowedSubject(certificate, settings.AllowedSubjects))
        {
            return false;
        }

        var anchors = GetAnchors(settings);

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(anchors);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = TimeProvider.GetUtcNow().UtcDateTime;
        return chain.Build(certificate);
    }

    private static bool HasClientAuthentication(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension is not X509EnhancedKeyUsageExtension usages)
            {
                continue;
            }

            foreach (var usage in usages.EnhancedKeyUsages)
            {
                if (string.Equals(usage.Value, ClientAuthenticationOid, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAllowedSubject(
        X509Certificate2 certificate,
        IReadOnlyList<string> allowedSubjects
    )
    {
        var subject = certificate.SubjectName.Name;
        foreach (var allowedSubject in allowedSubjects)
        {
            var allowedName = new X500DistinguishedName(allowedSubject).Name;
            if (string.Equals(subject, allowedName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private X509Certificate2Collection GetAnchors(ProxyClientCertificateOptions settings)
    {
        var path =
            settings.CertificateAuthorityPath
            ?? throw new InvalidOperationException(
                $"{ProxyClientCertificateOptions.DefaultSectionName}:{nameof(ProxyClientCertificateOptions.CertificateAuthorityPath)} is not set."
            );

        lock (Gate)
        {
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            if (Anchors is null || lastWriteTimeUtc != AnchorsLastWriteTimeUtc)
            {
                var anchors = new X509Certificate2Collection();
                anchors.ImportFromPemFile(path);
                Anchors = anchors;
                AnchorsLastWriteTimeUtc = lastWriteTimeUtc;
            }

            return Anchors;
        }
    }
}
