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

using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Checks certificate revocation with certificate revocation lists. It reads the <c>http</c> and <c>https</c>
/// locations in the certificate's CRL distribution points and fetches each list through
/// <see cref="RevocationDataFetcher"/>, the outbound-request guard. A list is used only when it is well formed,
/// current, within the scope of the distribution point it was fetched from, and signed by the issuing certificate or
/// by a signer that chains to the same trust anchor through certificates that are not revoked. A certificate is
/// <see cref="ClientCertificateRevocationStatus.Good"/> or <see cref="ClientCertificateRevocationStatus.Revoked"/>
/// only on the word of such lists, which together cover every reason; otherwise its status is
/// <see cref="ClientCertificateRevocationStatus.Unknown"/>. A fetched list is cached per location, parsed but not
/// trusted, until its next update, for at most the configured maximum cache duration; each check verifies the list
/// against its own signers, so a list retrieved for one authority is never trusted for another.
/// </summary>
// See ADR-0252.
internal sealed class CrlClientCertificateRevocationSource(
    TimeProvider timeProvider,
    RevocationDataFetcher fetcher,
    RevocationRetrievalCache retrievalCache,
    IOptions<ClientCertificateRevocationOptions> options,
    ILogger<CrlClientCertificateRevocationSource> logger
) : IClientCertificateRevocationSource
{
    private const string CacheKeyPrefix = "NCode.Identity.OpenId.ClientCertificateRevocation:crl:";
    private const string NoLocation = "(none)";

    private TimeProvider TimeProvider { get; } = timeProvider;
    private RevocationDataFetcher Fetcher { get; } = fetcher;
    private RevocationRetrievalCache RetrievalCache { get; } = retrievalCache;
    private ClientCertificateRevocationOptions Options { get; } = options.Value;
    private ILogger<CrlClientCertificateRevocationSource> Logger { get; } = logger;

    /// <inheritdoc />
    public string Name => ClientCertificateRevocationSourceNames.Crl;

    /// <inheritdoc />
    // Processes the certificate's distribution points as RFC 5280 §6.3.3 describes. See ADR-0259.
    public async ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.Depth > RevocationLimits.MaxNestedDepth)
        {
            Logger.RevocationInformationUnavailable(
                NoLocation,
                context.Certificate.Subject,
                "The check is nested more deeply than a signer's path is followed."
            );
            return ClientCertificateRevocationStatus.Unknown;
        }

        var certificate = context.Certificate;

        var points = CrlDistributionPoints.Read(certificate);
        if (points.Count == 0)
        {
            Logger.RevocationInformationUnavailable(
                NoLocation,
                certificate.Subject,
                "The certificate names no http or https CRL distribution point."
            );
            return ClientCertificateRevocationStatus.Unknown;
        }

        var isAuthority = certificate
            .Extensions.OfType<X509BasicConstraintsExtension>()
            .Any(extension => extension.CertificateAuthority);

        var serialNumber = new BigInteger(
            certificate.SerialNumberBytes.Span,
            isUnsigned: false,
            isBigEndian: true
        );

        var coveredReasons = 0;
        foreach (var point in points)
        {
            if (coveredReasons == CrlReasons.All)
                break;

            var outcome = await CheckPointAsync(
                context,
                point,
                isAuthority,
                serialNumber,
                coveredReasons,
                cancellationToken
            );
            if (outcome.IsRevoked)
                return ClientCertificateRevocationStatus.Revoked;

            coveredReasons |= outcome.CoveredReasons;
        }

        return coveredReasons == CrlReasons.All
            ? ClientCertificateRevocationStatus.Good
            : ClientCertificateRevocationStatus.Unknown;
    }

    private async ValueTask<CrlPointOutcome> CheckPointAsync(
        ClientCertificateRevocationContext context,
        CrlDistributionPoint point,
        bool isAuthority,
        BigInteger serialNumber,
        int coveredReasons,
        CancellationToken cancellationToken
    )
    {
        var certificate = context.Certificate;
        var subject = certificate.Subject;

        if (
            point.Reasons is { } pointReasons
            && (pointReasons & CrlReasons.All & ~coveredReasons) == 0
        )
            return CrlPointOutcome.Uncovered;

        foreach (var uri in point.Uris)
        {
            var crl = await GetListAsync(uri, subject, cancellationToken);
            if (crl is null)
                continue;

            if (crl.IsDelta)
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The list at a CRL distribution point is a delta list."
                );
                continue;
            }

            if (!crl.IsInScope(point, certificate.IssuerName, isAuthority))
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The list does not cover this certificate at this location."
                );
                continue;
            }

            var reasons = crl.GetCoveredReasons(point);
            if ((reasons & ~coveredReasons) == 0)
                return CrlPointOutcome.Uncovered;

            var signer = await FindTrustedSignerAsync(crl, context, cancellationToken);
            if (signer is null)
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The list is not signed by the issuing certificate or by a signer that chains to the same trust anchor."
                );
                continue;
            }

            var deltaUris = GetDeltaUris(certificate, crl);
            var delta =
                deltaUris.Count > 0
                    ? await GetDeltaAsync(deltaUris, crl, signer, subject, cancellationToken)
                    : null;
            if (delta?.FindEntry(serialNumber, certificate.IssuerName) is { } deltaEntry)
            {
                if (!deltaEntry.IsRemoveFromCrl)
                    return CrlPointOutcome.Revoked;
            }
            else if (
                crl.FindEntry(serialNumber, certificate.IssuerName) is { IsRemoveFromCrl: false }
            )
            {
                return CrlPointOutcome.Revoked;
            }

            if (deltaUris.Count > 0 && delta is null)
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The list names a delta list, and no usable delta list was found."
                );
                return CrlPointOutcome.Uncovered;
            }

            return new CrlPointOutcome(false, reasons);
        }

        return CrlPointOutcome.Uncovered;
    }

    private static IReadOnlyList<string> GetDeltaUris(
        X509Certificate2 certificate,
        CertificateRevocationList complete
    ) =>
        [
            .. CrlDistributionPoints
                .ReadFreshest(certificate)
                .SelectMany(point => point.Uris)
                .Concat(complete.FreshestCrlUris)
                .Distinct(StringComparer.Ordinal),
        ];

    // RFC 5280 §5.2.4: a delta list applies when it is based on the complete list and the complete list's signer
    // signed it.
    private async ValueTask<CertificateRevocationList?> GetDeltaAsync(
        IReadOnlyList<string> uris,
        CertificateRevocationList complete,
        X509Certificate2 signer,
        string subject,
        CancellationToken cancellationToken
    )
    {
        foreach (var uri in uris)
        {
            var delta = await GetListAsync(uri, subject, cancellationToken);
            if (delta is null)
                continue;

            if (!delta.IsDelta)
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The list at a freshest CRL location is not a delta list."
                );
                continue;
            }

            if (!complete.CanApplyDelta(delta))
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The delta list does not apply to the complete list: their issuer, scope, authority key or CRL numbers do not match."
                );
                continue;
            }

            if (!delta.IsSignedBy(signer))
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The delta list is not signed by the signer of the complete list."
                );
                continue;
            }

            return delta;
        }

        return null;
    }

    private async ValueTask<X509Certificate2?> FindTrustedSignerAsync(
        CertificateRevocationList crl,
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    )
    {
        if (crl.IsSignedBy(context.Issuer))
            return context.Issuer;

        X509Certificate2[] candidates = [context.TrustAnchor, .. context.Intermediates];
        foreach (var candidate in candidates)
        {
            if (
                candidate.RawData.AsSpan().SequenceEqual(context.Issuer.RawData)
                || !crl.IsSignedBy(candidate)
            )
            {
                continue;
            }

            if (await IsTrustedSignerAsync(candidate, context, cancellationToken))
                return candidate;
        }

        return null;
    }

    // RFC 5280 §6.3.3 (f): a path to the same trust anchor whose certificates are not revoked.
    private async ValueTask<bool> IsTrustedSignerAsync(
        X509Certificate2 signer,
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    )
    {
        if (signer.RawData.AsSpan().SequenceEqual(context.TrustAnchor.RawData))
            return true;

        using var chain = new X509Chain();
        try
        {
            var policy = chain.ChainPolicy;
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.CustomTrustStore.Add(context.TrustAnchor);
            foreach (var intermediate in context.Intermediates)
                policy.ExtraStore.Add(intermediate);

            policy.DisableCertificateDownloads = true;
            policy.RevocationMode = X509RevocationMode.NoCheck;
            policy.VerificationTime = TimeProvider.GetUtcNow().UtcDateTime;
            policy.VerificationTimeIgnored = false;

            if (!chain.Build(signer))
                return false;

            var elements = chain.ChainElements;
            for (var i = 0; i < elements.Count - 1; i++)
            {
                var status = await CheckAsync(
                    new ClientCertificateRevocationContext
                    {
                        Certificate = elements[i].Certificate,
                        Issuer = elements[i + 1].Certificate,
                        TrustAnchor = context.TrustAnchor,
                        Intermediates = context.Intermediates,
                        Depth = context.Depth + 1,
                    },
                    cancellationToken
                );
                if (status != ClientCertificateRevocationStatus.Good)
                    return false;
            }

            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            foreach (var element in chain.ChainElements)
                element.Certificate.Dispose();
        }
    }

    // A list fetched, parsed and current, shared by every check and not yet trusted by any.
    internal ValueTask<CertificateRevocationList?> GetListAsync(
        string uri,
        string subject,
        CancellationToken cancellationToken
    ) =>
        RetrievalCache.GetAsync(
            CacheKeyPrefix + uri,
            uri,
            subject,
            () => RetrieveAsync(uri, subject),
            cancellationToken
        );

    private async Task<RevocationRetrieval<CertificateRevocationList>> RetrieveAsync(
        string uri,
        string subject
    )
    {
        var encoded = await Fetcher.GetAsync(uri, uri, subject, CancellationToken.None);
        if (encoded is null)
            return default;

        if (!CertificateRevocationList.TryParse(encoded, out var crl))
        {
            Logger.RevocationInformationUnavailable(
                uri,
                subject,
                "The response is not a DER encoded certificate revocation list."
            );
            return default;
        }

        if (crl.HasUnsupportedFeatures)
        {
            Logger.RevocationInformationUnavailable(
                uri,
                subject,
                "The list uses a feature that is not supported."
            );
            return default;
        }

        var now = TimeProvider.GetUtcNow();
        if (crl.ThisUpdate > now + RevocationLimits.ClockSkew)
        {
            Logger.RevocationInformationUnavailable(uri, subject, "The list is not yet valid.");
            return default;
        }

        if (crl.NextUpdate is not { } nextUpdate || nextUpdate <= now)
        {
            Logger.RevocationInformationUnavailable(
                uri,
                subject,
                "The list is not current: it has no next update, or its next update has passed."
            );
            return default;
        }

        var maxExpiration = now + Options.MaxCacheDuration;
        return new RevocationRetrieval<CertificateRevocationList>(
            crl,
            nextUpdate < maxExpiration ? nextUpdate : maxExpiration
        );
    }
}
