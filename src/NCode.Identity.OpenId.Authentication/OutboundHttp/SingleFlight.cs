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
// See ADR-1315.
internal sealed class SingleFlight<TResult>(IMemoryCache memoryCache, TimeSpan failureCacheDuration)
{
    private const string FailureKeySuffix = ":unavailable";

    private IMemoryCache MemoryCache { get; } = memoryCache;

    /// <summary>
    /// Gets how long a failed retrieval is remembered.
    /// </summary>
    public TimeSpan FailureCacheDuration { get; } = failureCacheDuration;

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
    /// Remembers that a retrieval for <paramref name="key"/> failed, for <see cref="FailureCacheDuration"/>.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    public void RememberFailure(string key) =>
        MemoryCache.Set(key + FailureKeySuffix, true, FailureCacheDuration);

    /// <summary>
    /// Runs <paramref name="retrieve"/> for <paramref name="key"/>, or joins the retrieval that is already running
    /// for it.
    /// </summary>
    /// <param name="key">The key of the retrieval.</param>
    /// <param name="retrieve">The retrieval to run when none is running for <paramref name="key"/>; it receives its own token, never a caller's.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to stop waiting for the retrieval.</param>
    /// <returns>The value that the retrieval yielded.</returns>
    public async ValueTask<TResult> RunAsync(
        string key,
        Func<CancellationToken, ValueTask<TResult>> retrieve,
        CancellationToken cancellationToken
    )
    {
        // The in-flight entry stores a Task, because every caller sharing the retrieval awaits it. The retrieval runs on
        // CancellationToken.None so that no caller's cancellation faults it for the others.
        var flight = InFlight.GetOrAdd(
            key,
            _ => new Lazy<Task<TResult>>(() =>
                RunAndLeaveAsync(key, retrieve, CancellationToken.None).AsTask()
            )
        );
        return await flight.Value.WaitAsync(cancellationToken);
    }

    private async ValueTask<TResult> RunAndLeaveAsync(
        string key,
        Func<CancellationToken, ValueTask<TResult>> retrieve,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await retrieve(cancellationToken);
        }
        finally
        {
            InFlight.TryRemove(key, out _);
        }
    }
}
