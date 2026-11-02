// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Caches the retrieval of revocation information by key. A retrieved value is reused until the time its retrieval
/// names, concurrent callers of one key share a single retrieval, and a retrieval that yielded nothing usable is
/// remembered for <see cref="ClientCertificateRevocationOptions.FailureCacheDuration"/>.
/// </summary>
// See ADR-1252.
internal sealed class RevocationRetrievalCache(
    IMemoryCache memoryCache,
    IOptions<ClientCertificateRevocationOptions> options,
    ILogger<RevocationRetrievalCache> logger
)
{
    private IMemoryCache MemoryCache { get; } = memoryCache;
    private ILogger<RevocationRetrievalCache> Logger { get; } = logger;

    private SingleFlight<object?> Retrievals { get; } =
        new(memoryCache, options.Value.FailureCacheDuration);

    /// <summary>
    /// Determines whether a failed retrieval for <paramref name="key"/> is remembered.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    /// <returns><see langword="true"/> when the failure is remembered; otherwise, <see langword="false"/>.</returns>
    public bool IsUnavailable(string key) => Retrievals.IsFailureRemembered(key);

    /// <summary>
    /// Remembers that a retrieval for <paramref name="key"/> yielded nothing usable, for
    /// <see cref="ClientCertificateRevocationOptions.FailureCacheDuration"/>.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    public void MarkUnavailable(string key) => Retrievals.RememberFailure(key);

    /// <summary>
    /// Gets the cached value for <paramref name="key"/>, or runs <paramref name="retrieve"/> to obtain it.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    /// <param name="location">The location that the log entries name.</param>
    /// <param name="subject">The subject of the certificate that the log entries name.</param>
    /// <param name="retrieve">The retrieval to run when no value is cached and no failure is remembered.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to stop waiting for the retrieval.</param>
    /// <typeparam name="T">The type of the retrieved value.</typeparam>
    /// <returns>The value, or <see langword="null"/> when nothing usable is available.</returns>
    public async ValueTask<T?> GetAsync<T>(
        string key,
        string location,
        string subject,
        Func<Task<RevocationRetrieval<T>>> retrieve,
        CancellationToken cancellationToken
    )
        where T : class
    {
        // A retrieval that yielded nothing usable is remembered, so an unreachable or misbehaving source is not
        // asked again for every check. See ADR-1252.
        if (TryGetRemembered(key, out T? remembered))
        {
            if (remembered is null)
            {
                Logger.RevocationRetrievalSuppressed(
                    location,
                    subject,
                    Retrievals.FailureCacheDuration
                );
            }

            return remembered;
        }

        // Concurrent checks share one retrieval; it runs without the caller's token, so a caller that gives up does
        // not break it for the others. See ADR-1252 and ADR-1350.
        var value = await Retrievals.RunAsync(
            key,
            () => RetrieveAsync(key, retrieve),
            cancellationToken
        );
        return value as T;
    }

    // Returns true with the cached value or, for a remembered failure, with null; false when neither is cached.
    private bool TryGetRemembered<T>(string key, out T? value)
        where T : class
    {
        if (MemoryCache.TryGetValue<T>(key, out var cached) && cached is not null)
        {
            value = cached;
            return true;
        }

        value = null;
        return IsUnavailable(key);
    }

    private async Task<object?> RetrieveAsync<T>(
        string key,
        Func<Task<RevocationRetrieval<T>>> retrieve
    )
        where T : class
    {
        // A caller that checked the caches just before a previous retrieval recorded its result and left the in-flight
        // dictionary starts this one, so the caches are checked again before anything is downloaded. See ADR-1366.
        if (TryGetRemembered(key, out T? remembered))
            return remembered;

        var result = await retrieve();
        if (result.Value is null)
        {
            MarkUnavailable(key);
            return null;
        }

        if (result.CacheUntil is { } cacheUntil)
            MemoryCache.Set(key, result.Value, cacheUntil);

        return result.Value;
    }
}
