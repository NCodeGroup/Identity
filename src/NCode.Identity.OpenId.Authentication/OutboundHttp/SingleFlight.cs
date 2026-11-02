// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Shares one retrieval per key among concurrent callers, and remembers for a short time that a retrieval failed. A
/// retrieval runs without any caller's token and each caller stops waiting with its own token, so a caller that gives
/// up does not break the retrieval for the others. The owner decides what counts as a failure, caches its own values,
/// and logs.
/// </summary>
/// <typeparam name="TResult">The type of the value that a retrieval yields.</typeparam>
// See ADR-1350.
internal sealed class SingleFlight<TResult>(IMemoryCache memoryCache, TimeSpan failureCacheDuration)
{
    private const string FailureKeySuffix = ":unavailable";

    private IMemoryCache MemoryCache { get; } = memoryCache;
    private TimeSpan FailureCacheDuration { get; } = failureCacheDuration;

    private ConcurrentDictionary<string, Lazy<Task<TResult>>> InFlight { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Determines whether a failed retrieval for <paramref name="key"/> is remembered.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    /// <returns><see langword="true"/> when the failure is remembered; otherwise, <see langword="false"/>.</returns>
    public bool IsFailureRemembered(string key) =>
        MemoryCache.TryGetValue(key + FailureKeySuffix, out _);

    /// <summary>
    /// Remembers that a retrieval for <paramref name="key"/> failed, for the duration given to the constructor.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    public void RememberFailure(string key) =>
        MemoryCache.Set(key + FailureKeySuffix, true, FailureCacheDuration);

    /// <summary>
    /// Runs <paramref name="retrieve"/> for <paramref name="key"/>, or joins the retrieval that is already running
    /// for it.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    /// <param name="retrieve">The retrieval to run when none is running for <paramref name="key"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to stop waiting for the retrieval.</param>
    /// <returns>The value that the retrieval yielded.</returns>
    public async ValueTask<TResult> RunAsync(
        string key,
        Func<Task<TResult>> retrieve,
        CancellationToken cancellationToken
    )
    {
        var flight = InFlight.GetOrAdd(
            key,
            _ => new Lazy<Task<TResult>>(() => RunAndLeaveAsync(key, retrieve))
        );
        return await flight.Value.WaitAsync(cancellationToken);
    }

    // A Task, not a ValueTask, because every caller sharing the retrieval awaits it.
    private async Task<TResult> RunAndLeaveAsync(string key, Func<Task<TResult>> retrieve)
    {
        try
        {
            return await retrieve();
        }
        finally
        {
            InFlight.TryRemove(key, out _);
        }
    }
}
