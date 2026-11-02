// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Json;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientKeyResolver"/>. A client's public keys come from exactly
/// one source: the keys it registered by value (its stored <see cref="OpenIdClient.SecretKeys"/>) or, when it has a
/// <c>jwks_uri</c>, the keys it publishes there — never both. The secrets a client shares with the server (its
/// symmetric keys) are always included. The <c>jwks_uri</c> fetch is guarded against server-side request forgery (the
/// connection is pinned away from private/loopback/link-local addresses, redirects are not followed, an optional host
/// allow-list applies, and both a wall-clock timeout and a response-size cap bound the request), the fetched set is
/// imported under the client-key policy of <see cref="IClientKeyImporter"/>, and an accepted set is cached for the
/// configured duration. Concurrent misses share one fetch per <c>jwks_uri</c> and set of fetch limits, and a fetch that
/// yields no key is remembered for <see cref="OutboundHttpOptions.FailureCacheDuration"/>. A fetch that fails or a set
/// that is refused leaves the client with no public keys, so an unreachable, malformed, or unsafe key set never grants
/// access.
/// </summary>
// See ADR-1161.
internal sealed class DefaultClientKeyResolver(
    ILogger<DefaultClientKeyResolver> logger,
    TimeProvider timeProvider,
    IHttpClientFactory httpClientFactory,
    IOutboundHttpContentReader outboundHttpContentReader,
    IOptions<OutboundHttpOptions> outboundHttpOptions,
    IMemoryCache memoryCache,
    IClientKeyImporter clientKeyImporter,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : IClientKeyResolver
{
    private const string CacheKeyPrefix =
        "NCode.Identity.OpenId.Authentication.Clients.DefaultClientKeyResolver";

    private ILogger<DefaultClientKeyResolver> Logger { get; } = logger;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IOutboundHttpContentReader OutboundHttpContentReader { get; } =
        outboundHttpContentReader;
    private IMemoryCache MemoryCache { get; } = memoryCache;
    private IClientKeyImporter ClientKeyImporter { get; } = clientKeyImporter;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } =
        secretKeyCollectionFactory;

    private SingleFlight<IReadOnlyList<SecretKey>> Fetches { get; } =
        new(memoryCache, outboundHttpOptions.Value.FailureCacheDuration);

    /// <inheritdoc />
    public async ValueTask<ISecretKeyCollection> ResolveSecretKeysAsync(
        OpenIdClient openIdClient,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdClient.Settings;

        if (
            !settings.TryGetValue(OpenIdSettingKeys.JwksUri, out var jwksUri)
            || string.IsNullOrEmpty(jwksUri)
        )
        {
            // The client registered its keys by value; its stored keys are the whole set.
            return openIdClient.SecretKeys;
        }

        var jwksKeys = await ResolveJwksKeysAsync(openIdClient, jwksUri, cancellationToken);

        // The keys published at the jwks_uri replace any public key stored for the client (OpenID Connect Dynamic Client
        // Registration 1.0 §2), so only the secrets it shares with the server are kept beside them. A new non-owning
        // collection: neither the stored keys (owned by the client) nor the jwks_uri keys (owned by the cache) are
        // disposed by it.
        var sharedKeys = openIdClient.SecretKeys.Where(secretKey =>
            secretKey is not AsymmetricSecretKey
        );
        return SecretKeyCollectionFactory.Create([.. sharedKeys, .. jwksKeys]);
    }

    private async ValueTask<IReadOnlyList<SecretKey>> ResolveJwksKeysAsync(
        OpenIdClient openIdClient,
        string jwksUri,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdClient.Settings;

        if (
            !AbsoluteUri.TryParse(jwksUri, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
        {
            Logger.ClientJwksFetchFailed(
                openIdClient.ClientId,
                jwksUri,
                new InvalidOperationException("The 'jwks_uri' must be an absolute https URI.")
            );
            return [];
        }

        if (
            settings.TryGetValue(OpenIdSettingKeys.JwksUriAllowedHosts, out var allowedHosts)
            && allowedHosts.Count > 0
            && !allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
        )
        {
            Logger.ClientJwksFetchFailed(
                openIdClient.ClientId,
                jwksUri,
                new InvalidOperationException(
                    $"The 'jwks_uri' host '{uri.Host}' is not in the configured allow-list."
                )
            );
            return [];
        }

        var fetchKey = GetFetchKey(openIdClient, uri);
        var remembered = GetRememberedKeys(openIdClient, uri, fetchKey);
        if (remembered is not null)
            return remembered;

        // Concurrent misses share one fetch; it runs without the caller's token, so a caller that gives up does not
        // break it for the others. See ADR-1335 and ADR-1350.
        return await Fetches.RunAsync(
            fetchKey,
            () => RunFetchAsync(fetchKey, openIdClient, uri),
            cancellationToken
        );
    }

    // Returns the accepted key set or, for a remembered failure, an empty set; null when neither is cached.
    private IReadOnlyList<SecretKey>? GetRememberedKeys(
        OpenIdClient openIdClient,
        Uri uri,
        string fetchKey
    )
    {
        var cacheKey = GetCacheKey(uri);
        if (
            MemoryCache.TryGetValue<IReadOnlyList<SecretKey>>(cacheKey, out var cached)
            && cached is not null
        )
        {
            return cached;
        }

        // A fetch that yielded no key is remembered, so an unreachable or misbehaving jwks_uri is not fetched again for
        // every request. See ADR-1335.
        if (!Fetches.IsFailureRemembered(fetchKey))
            return null;

        Logger.ClientJwksFetchSuppressed(
            openIdClient.ClientId,
            uri.AbsoluteUri,
            Fetches.FailureCacheDuration
        );
        return [];
    }

    // A Task, not a ValueTask, because every caller sharing the fetch awaits it.
    private async Task<IReadOnlyList<SecretKey>> RunFetchAsync(
        string fetchKey,
        OpenIdClient openIdClient,
        Uri uri
    )
    {
        // A caller that checked the cache just before a previous fetch recorded its result and left the in-flight
        // dictionary starts this one, so the result is checked again before the jwks_uri is fetched. See ADR-1335.
        var remembered = GetRememberedKeys(openIdClient, uri, fetchKey);
        if (remembered is not null)
            return remembered;

        var keys = await FetchAndImportAsync(openIdClient, uri);

        if (keys.Count == 0)
        {
            Fetches.RememberFailure(fetchKey);
            return keys;
        }

        var cacheDuration = openIdClient.Settings.GetValue(OpenIdSettingKeys.JwksUriCacheDuration);
        if (cacheDuration > TimeSpan.Zero)
        {
            // The cached keys hold only public key material (no native handles to dispose), so a plain absolute
            // expiration is sufficient and there is no eviction cleanup to perform.
            var cacheKey = GetCacheKey(uri);
            MemoryCache.Set<IReadOnlyList<SecretKey>>(
                cacheKey,
                keys,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheDuration }
            );
        }

        return keys;
    }

    private async ValueTask<List<SecretKey>> FetchAndImportAsync(OpenIdClient openIdClient, Uri uri)
    {
        var settings = openIdClient.Settings;
        var timeout = settings.GetValue(OpenIdSettingKeys.JwksUriFetchTimeout);
        var maxSizeBytes = settings.GetValue(OpenIdSettingKeys.JwksUriMaxSizeBytes);

        using var timeoutSource = new CancellationTokenSource();
        if (timeout > TimeSpan.Zero)
            timeoutSource.CancelAfter(timeout);

        try
        {
            using var httpClient = HttpClientFactory.CreateClient(GuardedHttpClient.Name);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                Logger.ClientJwksFetchFailed(
                    openIdClient.ClientId,
                    uri.AbsoluteUri,
                    new InvalidOperationException(
                        $"The 'jwks_uri' document could not be fetched (HTTP {(int)response.StatusCode})."
                    )
                );
                return [];
            }

            using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
            var readResult = await OutboundHttpContentReader.ReadCappedAsync(
                response,
                buffer,
                maxSizeBytes,
                timeoutSource.Token
            );
            if (readResult != OutboundHttpReadResult.Completed)
            {
                Logger.ClientJwksFetchFailed(
                    openIdClient.ClientId,
                    uri.AbsoluteUri,
                    new InvalidOperationException(
                        "The 'jwks_uri' document exceeds the maximum allowed size."
                    )
                );
                return [];
            }

            return ImportKeys(openIdClient, uri, buffer.AsReadOnlySequence);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or OperationCanceledException)
        {
            Logger.ClientJwksFetchFailed(openIdClient.ClientId, uri.AbsoluteUri, exception);
            return [];
        }
    }

    private List<SecretKey> ImportKeys(
        OpenIdClient openIdClient,
        Uri uri,
        ReadOnlySequence<byte> content
    )
    {
        JsonElement keySet;
        try
        {
            keySet = JsonElement.Parse(content);
        }
        catch (JsonException exception)
        {
            Logger.ClientJwksFetchFailed(openIdClient.ClientId, uri.AbsoluteUri, exception);
            return [];
        }

        var createdWhen = TimeProvider.GetUtcNow();
        if (
            !ClientKeyImporter.TryImportPublishedKeySet(
                keySet,
                createdWhen,
                out var persistedSecrets,
                out var error
            )
        )
        {
            Logger.ClientJwksFetchFailed(
                openIdClient.ClientId,
                uri.AbsoluteUri,
                new InvalidOperationException(error)
            );
            return [];
        }

        return [.. persistedSecrets.Select(SecretSerializer.DeserializePublicSecret)];
    }

    private static string GetCacheKey(Uri uri) => $"{CacheKeyPrefix}:{uri.AbsoluteUri}";

    private static string GetFetchKey(OpenIdClient openIdClient, Uri uri)
    {
        var settings = openIdClient.Settings;
        var timeout = settings.GetValue(OpenIdSettingKeys.JwksUriFetchTimeout);
        var maxSizeBytes = settings.GetValue(OpenIdSettingKeys.JwksUriMaxSizeBytes);
        return $"{CacheKeyPrefix}:fetch:{timeout.Ticks}:{maxSizeBytes}:{uri.AbsoluteUri}";
    }
}
