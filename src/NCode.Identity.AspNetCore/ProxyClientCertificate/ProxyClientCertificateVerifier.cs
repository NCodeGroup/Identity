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
    /// Returns the reason the certificate is not the proxy's, or <see cref="ProxyClientCertificateRefusal.None"/> when it
    /// is. The checks run in a fixed order: validity period, extended key usage, subject and, last, the chain. The
    /// anchors are the certificate authorities the operator trusts, so an anchor that is itself an issuing certificate
    /// authority is trusted without its parents: a chain that stops at such an anchor is accepted, while any other chain
    /// failure is refused.
    /// </summary>
    internal ProxyClientCertificateRefusal Verify(X509Certificate2 certificate)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        if (
            now < certificate.NotBefore.ToUniversalTime()
            || now > certificate.NotAfter.ToUniversalTime()
        )
        {
            return ProxyClientCertificateRefusal.NotYetValidOrExpired;
        }

        if (!HasClientAuthentication(certificate))
        {
            return ProxyClientCertificateRefusal.MissingClientAuthUsage;
        }

        var settings = Options.Value;
        if (!IsAllowedSubject(certificate, settings.AllowedSubjects))
        {
            return ProxyClientCertificateRefusal.SubjectNotAllowed;
        }

        var anchors = GetAnchors(settings);

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(anchors);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = now;

        // the certificate is the peer's, so its authority information access extension is never followed
        chain.ChainPolicy.DisableCertificateDownloads = true;

        return chain.Build(certificate) || IsPartialChainEndingAtAnAnchor(chain, anchors)
            ? ProxyClientCertificateRefusal.None
            : ProxyClientCertificateRefusal.UntrustedChain;
    }

    private static bool IsPartialChainEndingAtAnAnchor(
        X509Chain chain,
        X509Certificate2Collection anchors
    )
    {
        foreach (var status in chain.ChainStatus)
        {
            if (!IsNoErrorOrPartialChain(status.Status))
            {
                return false;
            }
        }

        var elements = chain.ChainElements;
        var lastIndex = elements.Count - 1;
        if (lastIndex < 0)
        {
            return false;
        }

        for (var index = 0; index <= lastIndex; index++)
        {
            foreach (var status in elements[index].ChainElementStatus)
            {
                if (status.Status == X509ChainStatusFlags.NoError)
                {
                    continue;
                }

                if (status.Status != X509ChainStatusFlags.PartialChain || index != lastIndex)
                {
                    return false;
                }
            }
        }

        var thumbprint = elements[lastIndex].Certificate.Thumbprint;
        foreach (var anchor in anchors)
        {
            if (string.Equals(anchor.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNoErrorOrPartialChain(X509ChainStatusFlags status) =>
        status is X509ChainStatusFlags.NoError or X509ChainStatusFlags.PartialChain;

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
