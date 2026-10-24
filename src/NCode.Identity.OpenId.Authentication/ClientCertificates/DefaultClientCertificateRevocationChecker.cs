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

using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the default implementation of <see cref="IClientCertificateRevocationChecker"/>. It checks certificate
/// revocation lists only: it reads the <c>http</c> and <c>https</c> locations in the certificate's CRL distribution
/// points and fetches each list through the outbound-request guard (the connection is pinned away from
/// private/loopback/link-local addresses, redirects are not followed, and both a timeout and a size cap bound the
/// request). A list is used only when it is well formed, signed by the issuing certificate, current, and covers the
/// location it was fetched from. A certificate is <see cref="ClientCertificateRevocationStatus.Good"/> or
/// <see cref="ClientCertificateRevocationStatus.Revoked"/> only on the word of such a list; otherwise its status is
/// <see cref="ClientCertificateRevocationStatus.Unknown"/>. A verified list is cached per location and issuing
/// certificate until its next update, for at most the configured maximum cache duration. Concurrent checks of one
/// list share a single retrieval, which is bounded by its own timeout, and a retrieval that yielded no usable list is
/// remembered for a configured time.
/// </summary>
internal sealed class DefaultClientCertificateRevocationChecker(
    TimeProvider timeProvider,
    IHttpClientFactory httpClientFactory,
    IOutboundHttpContentReader outboundHttpContentReader,
    IMemoryCache memoryCache,
    IOptions<ClientCertificateRevocationOptions> options,
    ILogger<DefaultClientCertificateRevocationChecker> logger
) : IClientCertificateRevocationChecker
{
    internal const string HttpClientName = "NCode.Identity.OpenId.ClientCertificateRevocation";

    private const string UnavailableKeySuffix = ":unavailable";

    private static readonly TimeSpan ThisUpdateSkew = TimeSpan.FromMinutes(5);

    private TimeProvider TimeProvider { get; } = timeProvider;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IOutboundHttpContentReader OutboundHttpContentReader { get; } =
        outboundHttpContentReader;
    private IMemoryCache MemoryCache { get; } = memoryCache;
    private ClientCertificateRevocationOptions Options { get; } = options.Value;
    private ILogger<DefaultClientCertificateRevocationChecker> Logger { get; } = logger;

    private ConcurrentDictionary<
        string,
        Lazy<Task<CertificateRevocationList?>>
    > Retrievals { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        CancellationToken cancellationToken
    )
    {
        var uris = CrlDistributionPoints.GetUris(certificate);
        if (uris.Count == 0)
        {
            Logger.CertificateRevocationListUnavailable(
                "(none)",
                certificate.Subject,
                "The certificate names no http or https CRL distribution point."
            );
            return ClientCertificateRevocationStatus.Unknown;
        }

        var isAuthority = certificate
            .Extensions.OfType<X509BasicConstraintsExtension>()
            .Any(extension => extension.CertificateAuthority);

        foreach (var uri in uris)
        {
            var crl = await GetVerifiedCrlAsync(certificate, issuer, uri, cancellationToken);
            if (crl is null)
                continue;

            if (!crl.Covers(uri, isAuthority))
            {
                Logger.CertificateRevocationListUnavailable(
                    uri,
                    certificate.Subject,
                    "The list does not cover this certificate at this location."
                );
                continue;
            }

            return crl.IsRevoked(certificate)
                ? ClientCertificateRevocationStatus.Revoked
                : ClientCertificateRevocationStatus.Good;
        }

        return ClientCertificateRevocationStatus.Unknown;
    }

    private async ValueTask<CertificateRevocationList?> GetVerifiedCrlAsync(
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        string uri,
        CancellationToken cancellationToken
    )
    {
        // The key includes the issuer, so a CRL verified for one CA is never trusted for another.
        var cacheKey =
            $"{typeof(DefaultClientCertificateRevocationChecker).FullName}:{uri}:{issuer.GetCertHashString(HashAlgorithmName.SHA256)}";
        if (
            MemoryCache.TryGetValue<CertificateRevocationList>(cacheKey, out var cached)
            && cached is { NextUpdate: { } cachedNextUpdate }
            && cachedNextUpdate > TimeProvider.GetUtcNow()
        )
        {
            return cached;
        }

        // A retrieval that yielded no usable list is remembered, so an unreachable or misbehaving source is not
        // asked again for every check. See ADR-0252.
        var failureKey = $"{cacheKey}{UnavailableKeySuffix}";
        if (MemoryCache.TryGetValue(failureKey, out _))
        {
            Logger.CertificateRevocationListRetrievalSuppressed(
                uri,
                certificate.Subject,
                Options.FailureCacheDuration
            );
            return null;
        }

        // Concurrent checks of one list share a retrieval. It runs on a copy of the issuer's data and without the
        // caller's token, so a caller that gives up or disposes its certificate does not break it for the others.
        var issuerData = issuer.RawData;
        var subject = certificate.Subject;
        var retrieval = Retrievals.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<CertificateRevocationList?>>(() =>
                RetrieveAsync(cacheKey, subject, issuerData, uri)
            )
        );
        return await retrieval.Value.WaitAsync(cancellationToken);
    }

    // A Task, not a ValueTask, because every caller sharing the retrieval awaits it.
    private async Task<CertificateRevocationList?> RetrieveAsync(
        string cacheKey,
        string subject,
        byte[] issuerData,
        string uri
    )
    {
        try
        {
            using var issuer = X509CertificateLoader.LoadCertificate(issuerData);
            var crl = await RetrieveVerifiedCrlAsync(subject, issuer, uri, CancellationToken.None);
            if (crl is null)
            {
                MemoryCache.Set(
                    $"{cacheKey}{UnavailableKeySuffix}",
                    true,
                    Options.FailureCacheDuration
                );
                return null;
            }

            var maxExpiration = TimeProvider.GetUtcNow() + Options.MaxCacheDuration;
            MemoryCache.Set(
                cacheKey,
                crl,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration =
                        crl.NextUpdate is { } nextUpdate && nextUpdate < maxExpiration
                            ? nextUpdate
                            : maxExpiration,
                }
            );

            return crl;
        }
        finally
        {
            Retrievals.TryRemove(cacheKey, out _);
        }
    }

    private async ValueTask<CertificateRevocationList?> RetrieveVerifiedCrlAsync(
        string subject,
        X509Certificate2 issuer,
        string uri,
        CancellationToken cancellationToken
    )
    {
        var encoded = await FetchAsync(subject, uri, cancellationToken);
        if (encoded is null)
            return null;

        if (!CertificateRevocationList.TryParse(encoded, out var crl))
        {
            Logger.CertificateRevocationListUnavailable(
                uri,
                subject,
                "The response is not a DER encoded certificate revocation list."
            );
            return null;
        }

        if (crl.HasUnsupportedFeatures)
        {
            Logger.CertificateRevocationListUnavailable(
                uri,
                subject,
                "The list uses a feature that is not supported."
            );
            return null;
        }

        if (!crl.IsSignedBy(issuer))
        {
            Logger.CertificateRevocationListUnavailable(
                uri,
                subject,
                "The list is not signed by the issuing certificate."
            );
            return null;
        }

        var now = TimeProvider.GetUtcNow();
        if (crl.ThisUpdate > now + ThisUpdateSkew)
        {
            Logger.CertificateRevocationListUnavailable(uri, subject, "The list is not yet valid.");
            return null;
        }

        if (crl.NextUpdate is not { } nextUpdate || nextUpdate <= now)
        {
            Logger.CertificateRevocationListUnavailable(
                uri,
                subject,
                "The list is not current: it has no next update, or its next update has passed."
            );
            return null;
        }

        return crl;
    }

    private async ValueTask<byte[]?> FetchAsync(
        string subject,
        string uri,
        CancellationToken cancellationToken
    )
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var requestUri))
        {
            Logger.CertificateRevocationListUnavailable(
                uri,
                subject,
                "The location is not an absolute URI."
            );
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Options.FetchTimeout);

        try
        {
            using var httpClient = HttpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                Logger.CertificateRevocationListUnavailable(
                    uri,
                    subject,
                    $"The list could not be fetched (HTTP {(int)response.StatusCode})."
                );
                return null;
            }

            using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
            var readResult = await OutboundHttpContentReader.ReadCappedAsync(
                response,
                buffer,
                Options.MaxResponseSizeBytes,
                timeoutCts.Token
            );
            if (readResult != OutboundHttpReadResult.Completed)
            {
                Logger.CertificateRevocationListUnavailable(
                    uri,
                    subject,
                    "The list exceeds the maximum allowed size."
                );
                return null;
            }

            return buffer.AsReadOnlySequence.ToArray();
        }
        catch (HttpRequestException exception)
        {
            Logger.CertificateRevocationListUnavailable(
                uri,
                subject,
                $"The list could not be fetched: {exception.Message}"
            );
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.CertificateRevocationListUnavailable(uri, subject, "The fetch timed out.");
            return null;
        }
    }
}
