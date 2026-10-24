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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Checks certificate revocation with OCSP (RFC 6960). It asks each <c>http</c> and <c>https</c> responder that the
/// certificate's authority information access names, with one request for one certificate identified by SHA-1 hashes
/// of its issuer (RFC 5019): a <c>GET</c> when the URL is short enough, otherwise a <c>POST</c>. Every request goes
/// through <see cref="RevocationDataFetcher"/>, the outbound-request guard. A response is used only when it is well
/// formed, answers for the certificate, is current, and is signed by the issuing certificate or by a delegated
/// responder the issuing certificate authorized; the revocation of a delegated responder that lacks the
/// <c>id-pkix-ocsp-nocheck</c> extension is checked through <see cref="CrlClientCertificateRevocationSource"/>. A
/// verified answer is cached per responder, issuer, and serial number until its next update, for at most the
/// configured maximum cache duration; a response without a next update is not cached. A failure or an
/// <c>unknown</c> answer is <see cref="ClientCertificateRevocationStatus.Unknown"/>, which leaves the decision to the
/// sources that follow.
/// </summary>
// See ADR-0259.
internal sealed class OcspClientCertificateRevocationSource(
    TimeProvider timeProvider,
    RevocationDataFetcher fetcher,
    RevocationRetrievalCache retrievalCache,
    CrlClientCertificateRevocationSource crlSource,
    IOptions<ClientCertificateRevocationOptions> options,
    ILogger<OcspClientCertificateRevocationSource> logger
) : IClientCertificateRevocationSource
{
    private const string CacheKeyPrefix = "NCode.Identity.OpenId.ClientCertificateRevocation:ocsp:";
    private const string LocationKeyPrefix =
        "NCode.Identity.OpenId.ClientCertificateRevocation:ocsp-location:";

    // RFC 5019 §5: a GET request is used when the whole URL is at most 255 characters.
    private const int MaxGetRequestLength = 255;

    private TimeProvider TimeProvider { get; } = timeProvider;
    private RevocationDataFetcher Fetcher { get; } = fetcher;
    private RevocationRetrievalCache RetrievalCache { get; } = retrievalCache;
    private CrlClientCertificateRevocationSource CrlSource { get; } = crlSource;
    private ClientCertificateRevocationOptions Options { get; } = options.Value;
    private ILogger<OcspClientCertificateRevocationSource> Logger { get; } = logger;

    /// <inheritdoc />
    public string Name => ClientCertificateRevocationSourceNames.Ocsp;

    /// <inheritdoc />
    public async ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    )
    {
        // Most certificates name no responder, so this is not logged.
        var uris = GetOcspUris(context.Certificate);
        if (uris.Count == 0)
            return ClientCertificateRevocationStatus.Unknown;

        var certificateId = OcspCertificateId.Create(context.Certificate, context.Issuer);
        var subject = context.Certificate.Subject;
        var issuerData = context.Issuer.RawData;
        var issuerKey = context.Issuer.GetCertHashString(HashAlgorithmName.SHA256);
        var serialHex = Convert.ToHexString(context.Certificate.SerialNumberBytes.Span);

        foreach (var uri in uris)
        {
            // A responder that failed at the HTTP level is not asked again for any certificate until the failure is
            // forgotten. See ADR-0259.
            var locationKey = LocationKeyPrefix + uri;
            if (RetrievalCache.IsUnavailable(locationKey))
            {
                Logger.RevocationRetrievalSuppressed(uri, subject, Options.FailureCacheDuration);
                continue;
            }

            var status = await RetrievalCache.GetAsync(
                $"{CacheKeyPrefix}{uri}:{issuerKey}:{serialHex}",
                uri,
                subject,
                () => RetrieveAsync(uri, locationKey, subject, issuerData, certificateId),
                cancellationToken
            );
            if (status is null)
                continue;

            if (
                !status.UncheckedResponder.IsEmpty
                && !await IsResponderGoodAsync(
                    status.UncheckedResponder,
                    context,
                    cancellationToken
                )
            )
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The delegated responder's own certificate is not known to be unrevoked."
                );
                continue;
            }

            if (status.Status == ClientCertificateRevocationStatus.Unknown)
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The responder does not know the certificate."
                );
                continue;
            }

            return status.Status;
        }

        return ClientCertificateRevocationStatus.Unknown;
    }

    internal static IReadOnlyList<string> GetOcspUris(X509Certificate2 certificate)
    {
        var extension = certificate
            .Extensions.OfType<X509AuthorityInformationAccessExtension>()
            .FirstOrDefault();
        if (extension is null)
            return [];

        try
        {
            return [.. extension.EnumerateOcspUris().Where(IsHttp)];
        }
        catch (CryptographicException)
        {
            return [];
        }
    }

    private static bool IsHttp(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // A delegated responder that lacks id-pkix-ocsp-nocheck is checked by the CRL source only, so the check cannot
    // loop back through OCSP. See ADR-0259.
    private async ValueTask<bool> IsResponderGoodAsync(
        ReadOnlyMemory<byte> encoded,
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    )
    {
        using var responder = X509CertificateLoader.LoadCertificate(encoded.Span);

        var status = await CrlSource.CheckAsync(
            new ClientCertificateRevocationContext
            {
                Certificate = responder,
                Issuer = context.Issuer,
                TrustAnchor = context.TrustAnchor,
                Intermediates = context.Intermediates,
                Depth = context.Depth + 1,
            },
            cancellationToken
        );
        return status == ClientCertificateRevocationStatus.Good;
    }

    private async Task<RevocationRetrieval<OcspVerifiedStatus>> RetrieveAsync(
        string uri,
        string locationKey,
        string subject,
        byte[] issuerData,
        OcspCertificateId certificateId
    )
    {
        using var issuer = X509CertificateLoader.LoadCertificate(issuerData);

        var request = OcspRequest.Encode(certificateId);
        var getUri =
            (uri.EndsWith('/') ? uri : uri + "/")
            + Uri.EscapeDataString(Convert.ToBase64String(request));
        var encoded =
            getUri.Length <= MaxGetRequestLength
                ? await Fetcher.GetAsync(getUri, uri, subject, CancellationToken.None)
                : await Fetcher.PostAsync(
                    uri,
                    subject,
                    request,
                    OcspRequest.ContentType,
                    CancellationToken.None
                );
        if (encoded is null)
        {
            RetrievalCache.MarkUnavailable(locationKey);
            return default;
        }

        if (!OcspResponse.TryParse(encoded, out var response))
        {
            Logger.RevocationInformationUnavailable(
                uri,
                subject,
                "The response is not a DER encoded OCSP response."
            );
            return default;
        }

        if (response.ResponseStatus != 0)
        {
            Logger.RevocationInformationUnavailable(
                uri,
                subject,
                $"The responder answered with status {response.ResponseStatus}."
            );
            return default;
        }

        if (
            response.HasUnsupportedFeatures
            || response.FindResponse(certificateId) is not { HasUnsupportedFeatures: false } single
        )
        {
            Logger.RevocationInformationUnavailable(
                uri,
                subject,
                "The response does not answer for this certificate, or uses a feature that is not supported."
            );
            return default;
        }

        var now = TimeProvider.GetUtcNow();
        if (!response.TryVerifySignature(issuer, now, out var uncheckedResponder))
        {
            Logger.RevocationInformationUnavailable(
                uri,
                subject,
                "The response is not signed by the issuing certificate or by a responder it authorized."
            );
            return default;
        }

        if (single.ThisUpdate > now + RevocationLimits.ClockSkew)
        {
            Logger.RevocationInformationUnavailable(uri, subject, "The response is not yet valid.");
            return default;
        }

        DateTimeOffset? cacheUntil;
        if (single.NextUpdate is { } nextUpdate)
        {
            if (nextUpdate <= now)
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The response is not current: its next update has passed."
                );
                return default;
            }

            var maxExpiration = now + Options.MaxCacheDuration;
            cacheUntil = nextUpdate < maxExpiration ? nextUpdate : maxExpiration;
        }
        else
        {
            // Without a next update the response is only as fresh as its production time, so it is not cached.
            if (single.ThisUpdate < now - RevocationLimits.ClockSkew)
            {
                Logger.RevocationInformationUnavailable(
                    uri,
                    subject,
                    "The response is not current: it has no next update and was produced more than five minutes ago."
                );
                return default;
            }

            cacheUntil = null;
        }

        return new RevocationRetrieval<OcspVerifiedStatus>(
            new OcspVerifiedStatus(single.Status, uncheckedResponder),
            cacheUntil
        );
    }
}
