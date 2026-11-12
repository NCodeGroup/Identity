// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore.ProxyClientCertificate;

/// <summary>
/// Judges whether a certificate is the reverse proxy's: it chains to the certificate authorities in
/// <see cref="ProxyClientCertificateOptions.CertificateAuthorityPath"/>, has the client authentication extended key
/// usage, is inside its validity period and has one of <see cref="ProxyClientCertificateOptions.AllowedSubjects"/> as its
/// subject. Revocation is not checked. The certificate authorities are read again when the file's last-write time
/// changes, which is checked at most once per <see cref="AnchorRefreshInterval"/>. A verdict is remembered per
/// certificate until the earlier of the certificate's validity boundary, <see cref="VerdictLifetime"/> after it was
/// made, or the next change of the certificate authorities.
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

    /// <summary>
    /// The most entries the verdict cache holds.
    /// </summary>
    internal const int MaxCachedVerdicts = 256;

    /// <summary>
    /// How often the last-write time of the certificate authority file is checked.
    /// </summary>
    internal static TimeSpan AnchorRefreshInterval { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The longest a verdict is remembered.
    /// </summary>
    internal static TimeSpan VerdictLifetime { get; } = TimeSpan.FromMinutes(5);

    private IOptions<ProxyClientCertificateOptions> Options { get; } = options;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private string[] AllowedNames { get; } =
    [.. options.Value.AllowedSubjects.Select(x => new X500DistinguishedName(x).Name)];
    private Lock Gate { get; } = new();
    private DateTime AnchorsLastWriteTimeUtc { get; set; }

    private ConcurrentDictionary<
        string,
        (int Generation, ProxyClientCertificateRefusal Refusal, DateTime ExpiresUtc)
    > Verdicts { get; } = new(StringComparer.Ordinal);

    private X509Certificate2Collection? _anchors;
    private int _anchorGeneration;
    private long _anchorRefreshDeadlineTicks;

    /// <summary>
    /// Returns the reason the certificate is not the proxy's, or <see cref="ProxyClientCertificateRefusal.None"/> when it
    /// is. The checks run in a fixed order: validity period, extended key usage, subject and, last, the chain. The
    /// anchors are the certificate authorities the operator trusts, so an anchor that is itself an issuing certificate
    /// authority is trusted without its parents: a chain that stops at such an anchor is accepted, while any other chain
    /// failure is refused.
    /// </summary>
    internal ProxyClientCertificateRefusal Verify(X509Certificate2 certificate)
    {
        var settings = Options.Value;
        var path =
            settings.CertificateAuthorityPath
            ?? throw new InvalidOperationException(
                $"{ProxyClientCertificateOptions.DefaultSectionName}:{nameof(ProxyClientCertificateOptions.CertificateAuthorityPath)} is not set."
            );

        var now = TimeProvider.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        RefreshAnchorsIfDue(path, now);

        // the generation is read before the anchors are, so a verdict is never filed under a generation newer than the
        // anchors it was made with
        var generation = Volatile.Read(ref _anchorGeneration);
        var thumbprint = certificate.Thumbprint;
        if (
            Verdicts.TryGetValue(thumbprint, out var cached)
            && cached.Generation == generation
            && cached.ExpiresUtc > nowUtc
        )
        {
            return cached.Refusal;
        }

        var refusal = Evaluate(certificate, nowUtc);

        var notBefore = certificate.NotBefore.ToUniversalTime();
        var boundary = nowUtc < notBefore ? notBefore : certificate.NotAfter.ToUniversalTime();
        var lifetimeEnd = nowUtc + VerdictLifetime;
        var expiresUtc = boundary < lifetimeEnd ? boundary : lifetimeEnd;
        if (expiresUtc > nowUtc)
        {
            MakeRoom(generation, nowUtc);
            Verdicts[thumbprint] = (generation, refusal, expiresUtc);
        }

        return refusal;
    }

    private void MakeRoom(int generation, DateTime nowUtc)
    {
        if (Verdicts.Count < MaxCachedVerdicts)
        {
            return;
        }

        foreach (var entry in Verdicts)
        {
            if (entry.Value.Generation != generation || entry.Value.ExpiresUtc <= nowUtc)
            {
                Verdicts.TryRemove(entry);
            }
        }

        if (Verdicts.Count >= MaxCachedVerdicts)
        {
            Verdicts.Clear();
        }
    }

    private ProxyClientCertificateRefusal Evaluate(X509Certificate2 certificate, DateTime now)
    {
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

        if (!IsAllowedSubject(certificate))
        {
            return ProxyClientCertificateRefusal.SubjectNotAllowed;
        }

        var anchors =
            Volatile.Read(ref _anchors)
            ?? throw new InvalidOperationException("The certificate authorities are not loaded.");

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(anchors);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = now;

        // the certificate is the peer's, so its authority information access extension is never followed
        chain.ChainPolicy.DisableCertificateDownloads = true;

        if (!chain.Build(certificate) && !IsPartialChainEndingAtAnAnchor(chain, anchors))
        {
            return ProxyClientCertificateRefusal.UnanchoredChain;
        }

        // platforms differ on whether they check the validity of a trust anchor, so the verifier checks it itself
        var anchor = chain.ChainElements[^1].Certificate;
        return now < anchor.NotBefore.ToUniversalTime() || now > anchor.NotAfter.ToUniversalTime()
            ? ProxyClientCertificateRefusal.UnanchoredChain
            : ProxyClientCertificateRefusal.None;
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

    private bool IsAllowedSubject(X509Certificate2 certificate)
    {
        var subject = certificate.SubjectName.Name;
        foreach (var allowedName in AllowedNames)
        {
            if (string.Equals(subject, allowedName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshAnchorsIfDue(string path, DateTimeOffset now)
    {
        // only the thread that moves the deadline checks the file's last-write time, and it does so outside the lock
        var deadlineTicks = Interlocked.Read(ref _anchorRefreshDeadlineTicks);
        var isDue =
            now.UtcTicks >= deadlineTicks
            && Interlocked.CompareExchange(
                ref _anchorRefreshDeadlineTicks,
                (now + AnchorRefreshInterval).UtcTicks,
                deadlineTicks
            ) == deadlineTicks;

        DateTime? checkedLastWriteTimeUtc = isDue ? File.GetLastWriteTimeUtc(path) : null;
        if (checkedLastWriteTimeUtc is null && Volatile.Read(ref _anchors) is not null)
        {
            return;
        }

        lock (Gate)
        {
            var lastWriteTimeUtc = checkedLastWriteTimeUtc ?? File.GetLastWriteTimeUtc(path);
            if (
                Volatile.Read(ref _anchors) is not null
                && lastWriteTimeUtc == AnchorsLastWriteTimeUtc
            )
            {
                return;
            }

            var anchors = new X509Certificate2Collection();
            anchors.ImportFromPemFile(path);
            AnchorsLastWriteTimeUtc = lastWriteTimeUtc;

            // the anchors are published before the generation, so a reader of the new generation sees the new anchors
            Volatile.Write(ref _anchors, anchors);
            Interlocked.Increment(ref _anchorGeneration);
        }
    }
}
