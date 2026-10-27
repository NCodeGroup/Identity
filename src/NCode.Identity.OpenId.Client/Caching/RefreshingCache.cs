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
