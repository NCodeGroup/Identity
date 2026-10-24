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
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientKeyResolver"/>. A client's public keys come from exactly
/// one source: the keys it registered by value (its stored <see cref="OpenIdClient.SecretKeys"/>) or, when it has a
/// <c>jwks_uri</c>, the keys it publishes there — never both. The secrets a client shares with the server (its
/// symmetric keys) are always included. The <c>jwks_uri</c> fetch is guarded against server-side request forgery (the
/// connection is pinned away from private/loopback/link-local addresses, redirects are not followed, an optional host
/// allow-list applies, and both a wall-clock timeout and a response-size cap bound the request), the fetched set is
/// imported under the client-key policy of <see cref="IClientKeyImporter"/>, and an accepted set is cached for the
/// configured duration. A fetch that fails or a set that is refused leaves the client with no public keys, so an
/// unreachable, malformed, or unsafe key set never grants access.
/// </summary>
// See ADR-0161.
internal sealed class DefaultClientKeyResolver(
    ILogger<DefaultClientKeyResolver> logger,
    TimeProvider timeProvider,
    IHttpClientFactory httpClientFactory,
    IOutboundHttpContentReader outboundHttpContentReader,
    IMemoryCache memoryCache,
    IClientKeyImporter clientKeyImporter,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : IClientKeyResolver
{
    internal const string HttpClientName = "NCode.Identity.OpenId.ClientJwks";

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
            !Uri.TryCreate(jwksUri, UriKind.Absolute, out var uri)
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

        var cacheKey = GetCacheKey(uri);
        if (
            MemoryCache.TryGetValue<IReadOnlyList<SecretKey>>(cacheKey, out var cached)
            && cached is not null
        )
        {
            return cached;
        }

        var keys = await FetchAndImportAsync(openIdClient, uri, cancellationToken);

        var cacheDuration = settings.GetValue(OpenIdSettingKeys.JwksUriCacheDuration);
        if (keys.Count > 0 && cacheDuration > TimeSpan.Zero)
        {
            // The cached keys hold only public key material (no native handles to dispose), so a plain absolute
            // expiration is sufficient and there is no eviction cleanup to perform.
            MemoryCache.Set<IReadOnlyList<SecretKey>>(
                cacheKey,
                keys,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheDuration }
            );
        }

        return keys;
    }

    private async ValueTask<List<SecretKey>> FetchAndImportAsync(
        OpenIdClient openIdClient,
        Uri uri,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdClient.Settings;
        var timeout = settings.GetValue(OpenIdSettingKeys.JwksUriFetchTimeout);
        var maxSizeBytes = settings.GetValue(OpenIdSettingKeys.JwksUriMaxSizeBytes);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero)
            timeoutCts.CancelAfter(timeout);

        try
        {
            using var httpClient = HttpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token
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
                timeoutCts.Token
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
            using var document = JsonDocument.Parse(content);
            keySet = document.RootElement.Clone();
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

    private static string GetCacheKey(Uri uri) =>
        $"NCode.Identity.OpenId.Authentication.Clients.DefaultClientKeyResolver:{uri.AbsoluteUri}";
}
