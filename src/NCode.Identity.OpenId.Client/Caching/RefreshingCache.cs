// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace NCode.Identity.OpenId.Client.Caching;

/// <summary>
/// A per-key cache that refreshes values on an interval, collapses concurrent fetches into one,
/// and serves the last good value for a while when a refresh fails.
/// </summary>
internal sealed class RefreshingCache<TValue>(TimeProvider timeProvider)
    where TValue : class
{
    private TimeProvider TimeProvider { get; } = timeProvider;

    private ConcurrentDictionary<string, RefreshingCacheEntry<TValue>> Entries { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the cached value when it is fresh or a recent attempt is backing off, otherwise fetches it.
    /// </summary>
    public ValueTask<TValue> GetAsync(
        string key,
        RefreshingCachePolicy policy,
        Func<CancellationToken, ValueTask<TValue>> fetchAsync,
        CancellationToken cancellationToken
    )
    {
        var entry = Entries.GetOrAdd(key, static _ => new RefreshingCacheEntry<TValue>());
        var now = TimeProvider.GetUtcNow();

        var value = entry.Value;
        if (value is not null)
        {
            if (now - entry.FetchedAt < policy.RefreshInterval)
                return ValueTask.FromResult(value);

            if (
                now - entry.LastAttemptAt < policy.MinimumRefreshInterval
                && now - entry.FetchedAt < policy.StaleLifetime
            )
                return ValueTask.FromResult(value);
        }

        return FetchAsync(entry, policy, fetchAsync, cancellationToken);
    }

    /// <summary>
    /// Fetches the value unless an attempt was made within the minimum refresh interval.
    /// </summary>
    public ValueTask<TValue> RefreshAsync(
        string key,
        RefreshingCachePolicy policy,
        Func<CancellationToken, ValueTask<TValue>> fetchAsync,
        CancellationToken cancellationToken
    )
    {
        var entry = Entries.GetOrAdd(key, static _ => new RefreshingCacheEntry<TValue>());
        var now = TimeProvider.GetUtcNow();

        var value = entry.Value;
        if (value is not null && now - entry.LastAttemptAt < policy.MinimumRefreshInterval)
            return ValueTask.FromResult(value);

        return FetchAsync(entry, policy, fetchAsync, cancellationToken);
    }

    private async ValueTask<TValue> FetchAsync(
        RefreshingCacheEntry<TValue> entry,
        RefreshingCachePolicy policy,
        Func<CancellationToken, ValueTask<TValue>> fetchAsync,
        CancellationToken cancellationToken
    )
    {
        var startedAt = TimeProvider.GetUtcNow();
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            var current = entry.Value;
            if (
                current is not null
                && entry.LastAttemptAt >= startedAt
                && TimeProvider.GetUtcNow() - entry.FetchedAt < policy.StaleLifetime
            )
                return current;

            entry.LastAttemptAt = TimeProvider.GetUtcNow();
            try
            {
                var fetched = await fetchAsync(cancellationToken);
                entry.Value = fetched;
                entry.FetchedAt = TimeProvider.GetUtcNow();
                return fetched;
            }
            catch (OpenIdProviderException)
                when (entry.Value is not null
                    && TimeProvider.GetUtcNow() - entry.FetchedAt < policy.StaleLifetime
                )
            {
                return entry.Value;
            }
        }
        finally
        {
            entry.Gate.Release();
        }
    }
}
