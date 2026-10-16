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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientKeyResolver"/>. It merges the client's stored
/// <see cref="OpenIdClient.SecretKeys"/> with the public keys resolved at runtime from the client's <c>jwks_uri</c>
/// setting. The <c>jwks_uri</c> fetch is guarded against server-side request forgery (the connection is pinned away
/// from private/loopback/link-local addresses, redirects are not followed, an optional host allow-list applies, and
/// both a wall-clock timeout and a response-size cap bound the request), and a successful fetch is cached for the
/// configured duration. A fetch or parse failure falls back to the client's stored keys only, so an unreachable or
/// malformed key set never grants access.
/// </summary>
internal sealed class DefaultClientKeyResolver(
    ILogger<DefaultClientKeyResolver> logger,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IHttpClientFactory httpClientFactory,
    IOutboundHttpContentReader outboundHttpContentReader,
    IMemoryCache memoryCache,
    ISecretImporter secretImporter,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : IClientKeyResolver
{
    internal const string HttpClientName = "NCode.Identity.OpenId.ClientJwks";

    private ILogger<DefaultClientKeyResolver> Logger { get; } = logger;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IOutboundHttpContentReader OutboundHttpContentReader { get; } =
        outboundHttpContentReader;
    private IMemoryCache MemoryCache { get; } = memoryCache;
    private ISecretImporter SecretImporter { get; } = secretImporter;
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
            // The client published no jwks_uri; its stored keys are the whole set.
            return openIdClient.SecretKeys;
        }

        var jwksKeys = await ResolveJwksKeysAsync(openIdClient, jwksUri, cancellationToken);
        if (jwksKeys.Count == 0)
        {
            return openIdClient.SecretKeys;
        }

        // A new non-owning collection over both sources; neither the stored keys (owned by the client) nor the
        // jwks_uri keys (owned by the cache) are disposed by this merged view.
        return SecretKeyCollectionFactory.Create([.. openIdClient.SecretKeys, .. jwksKeys]);
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

            return ImportKeys(openIdClient, buffer.AsReadOnlySequence);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or OperationCanceledException)
        {
            Logger.ClientJwksFetchFailed(openIdClient.ClientId, uri.AbsoluteUri, exception);
            return [];
        }
    }

    private List<SecretKey> ImportKeys(OpenIdClient openIdClient, ReadOnlySequence<byte> content)
    {
        JsonElement keysElement;
        try
        {
            using var document = JsonDocument.Parse(content);
            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("keys", out var keys)
                || keys.ValueKind != JsonValueKind.Array
            )
            {
                return [];
            }

            keysElement = keys.Clone();
        }
        catch (JsonException exception)
        {
            Logger.ClientJwksFetchFailed(openIdClient.ClientId, "(parsed document)", exception);
            return [];
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var secretKeys = new List<SecretKey>();

        foreach (var jwk in keysElement.EnumerateArray())
        {
            var secretKey = ImportKeyOrNull(openIdClient, jwk, createdWhen);
            if (secretKey is not null)
                secretKeys.Add(secretKey);
        }

        return secretKeys;
    }

    private SecretKey? ImportKeyOrNull(
        OpenIdClient openIdClient,
        JsonElement jwk,
        DateTimeOffset createdWhen
    )
    {
        if (
            jwk.ValueKind != JsonValueKind.Object
            || !jwk.TryGetProperty("kty", out var ktyElement)
            || ktyElement.ValueKind != JsonValueKind.String
        )
        {
            return null;
        }

        var secretType = ktyElement.GetString() switch
        {
            "RSA" => SecretTypes.Rsa,
            "EC" => SecretTypes.Ecc,
            _ => null,
        };
        if (secretType is null)
        {
            return null;
        }

        var keyId = ReadStringMember(jwk, "kid");
        if (string.IsNullOrEmpty(keyId))
        {
            keyId = CryptoService.GenerateResourceId();
        }

        try
        {
            var persistedSecret = SecretImporter.ImportSecret(
                new SecretKeyImportRequest
                {
                    SecretId = keyId,
                    SecretType = secretType,
                    Format = SecretKeyImportFormat.Jwk,
                    KeyMaterial = jwk.GetRawText(),
                    Use = ReadStringMember(jwk, "use"),
                    Algorithm = ReadStringMember(jwk, "alg"),
                    CreatedWhen = createdWhen,
                    ExpiresWhen = DateTimeOffset.MaxValue,
                }
            );

            return SecretSerializer.DeserializeSecret(persistedSecret);
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or System.Security.Cryptography.CryptographicException
            )
        {
            // A malformed key in an otherwise-usable set is skipped rather than failing the whole resolution.
            Logger.ClientJwksKeyImportFailed(openIdClient.ClientId, exception);
            return null;
        }
    }

    private static string GetCacheKey(Uri uri) =>
        $"NCode.Identity.OpenId.Authentication.Clients.DefaultClientKeyResolver:{uri.AbsoluteUri}";

    private static string? ReadStringMember(JsonElement element, string memberName) =>
        element.TryGetProperty(memberName, out var member)
        && member.ValueKind == JsonValueKind.String
            ? member.GetString()
            : null;
}
