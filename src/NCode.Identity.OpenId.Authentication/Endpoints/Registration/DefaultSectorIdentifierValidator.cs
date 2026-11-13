// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Provides the default implementation of <see cref="ISectorIdentifierValidator"/>. It fetches the JSON document
/// referenced by the client's <c>sector_identifier_uri</c> and verifies it lists every one of the client's redirect
/// URIs (OpenID Connect Core 1.0 §8.1). The fetch is guarded against server-side request forgery: the URI must be
/// <c>https</c>, its host must satisfy the optional <c>sector_identifier_uri_allowed_hosts</c> allow-list, the
/// connection is pinned away from private/loopback/link-local addresses by
/// <see cref="NCode.Identity.OpenId.Authentication.OutboundHttp.DefaultOutboundHttpForgeryGuard"/>, redirects are not followed, and
/// both a wall-clock timeout and a response size cap bound the request.
/// </summary>
internal class DefaultSectorIdentifierValidator(
    ILogger<DefaultSectorIdentifierValidator> logger,
    IHttpClientFactory httpClientFactory,
    IOutboundHttpContentReader outboundHttpContentReader
) : ISectorIdentifierValidator
{
    private ILogger Logger { get; } = logger;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IOutboundHttpContentReader OutboundHttpContentReader { get; } =
        outboundHttpContentReader;

    /// <inheritdoc />
    public async ValueTask<IOpenIdError?> ValidateAsync(
        OpenIdContext openIdContext,
        string sectorIdentifierUri,
        IReadOnlyCollection<string> redirectUris,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdContext.Tenant.SettingsProvider.Collection;

        IOpenIdError InvalidMetadata(string description) =>
            errorFactory.InvalidRequest(
                description,
                OpenIdProtocol.ErrorCodes.InvalidClientMetadata
            );

        // §8.1: the sector_identifier_uri must be an https URL.
        if (
            !AbsoluteUri.TryParse(sectorIdentifierUri, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
        {
            return InvalidMetadata("The 'sector_identifier_uri' must be an absolute https URI.");
        }

        // Optional SSRF allow-list: when configured, the host must be listed (the always-on private-address block in
        // DefaultOutboundHttpForgeryGuard applies regardless).
        if (
            settings.TryGetValue(
                OpenIdSettingKeys.SectorIdentifierUriAllowedHosts,
                out var allowedHosts
            )
            && allowedHosts.Count > 0
            && !allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
        )
        {
            return InvalidMetadata(
                $"The 'sector_identifier_uri' host '{uri.Host}' is not in the configured allow-list."
            );
        }

        var timeout = settings.GetValue(OpenIdSettingKeys.SectorIdentifierUriFetchTimeout);
        var maxSizeBytes = settings.GetValue(OpenIdSettingKeys.SectorIdentifierUriMaxSizeBytes);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero)
            timeoutCts.CancelAfter(timeout);

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        try
        {
            using var httpClient = HttpClientFactory.CreateClient(GuardedHttpClient.Name);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                return InvalidMetadata(
                    $"The 'sector_identifier_uri' document could not be fetched (HTTP {(int)response.StatusCode})."
                );
            }

            var readResult = await OutboundHttpContentReader.ReadCappedAsync(
                response,
                buffer,
                maxSizeBytes,
                timeoutCts.Token
            );

            if (readResult != OutboundHttpReadResult.Completed)
            {
                return InvalidMetadata(
                    "The 'sector_identifier_uri' document exceeds the maximum allowed size."
                );
            }
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            var uriForLog = OutboundUriRedaction.ForLog(uri);
            var reason = OutboundFetchFailureReasons.Classify(exception, timedOut: true);
            Logger.SectorIdentifierUriFetchFailed(uriForLog, reason, exception);
            return InvalidMetadata(
                "The 'sector_identifier_uri' document could not be fetched within the allotted time."
            );
        }
        catch (Exception exception)
            when (OutboundFetchFailureReasons.IsFetchFailure(exception, cancellationToken))
        {
            var timedOut = OutboundFetchFailureReasons.IsTimedOut(timeoutCts, cancellationToken);
            var uriForLog = OutboundUriRedaction.ForLog(uri);
            var reason = OutboundFetchFailureReasons.Classify(exception, timedOut);
            Logger.SectorIdentifierUriFetchFailed(uriForLog, reason, exception);
            return InvalidMetadata("The 'sector_identifier_uri' document could not be fetched.");
        }

        if (!TryParseDocument(buffer.AsReadOnlySequence, out var documentUris))
        {
            return InvalidMetadata(
                "The 'sector_identifier_uri' document is not a JSON array of redirect URI strings."
            );
        }

        var documentSet = new HashSet<string>(documentUris, StringComparer.Ordinal);
        foreach (var redirectUri in redirectUris)
        {
            if (!documentSet.Contains(redirectUri))
            {
                return InvalidMetadata(
                    $"The 'sector_identifier_uri' document does not include the redirect URI '{redirectUri}'."
                );
            }
        }

        return null;
    }

    private static bool TryParseDocument(
        ReadOnlySequence<byte> content,
        out List<string> redirectUris
    )
    {
        redirectUris = [];

        try
        {
            var jsonElement = JsonElement.Parse(content);
            if (jsonElement.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var element in jsonElement.EnumerateArray())
            {
                if (!element.TryGetValue<string>(out var value))
                    return false;

                redirectUris.Add(value);
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
