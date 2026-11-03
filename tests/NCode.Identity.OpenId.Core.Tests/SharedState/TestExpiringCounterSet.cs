// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Core.Tests.SharedState;

/// <summary>
/// An in-memory <see cref="IExpiringCounterSet"/> for unit tests that need counters which behave like the real ones:
/// a counter starts at one with the expiry of the increment that starts it, keeps that expiry while it is live, and is
/// absent once the time provider passes it. It counts the reads it serves, so a test can assert how many round trips a
/// caller made.
/// </summary>
/// <param name="timeProvider">The time provider that decides whether a counter has expired.</param>
public sealed class TestExpiringCounterSet(TimeProvider timeProvider) : IExpiringCounterSet
{
    private Dictionary<string, (long Count, DateTimeOffset ExpiresWhen)> Counters { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of calls to <see cref="GetAsync"/>.
    /// </summary>
    public int ReadCount { get; private set; }

    /// <summary>
    /// Gets the number of calls that incremented counters.
    /// </summary>
    public int IncrementCallCount { get; private set; }

    /// <inheritdoc />
    public ValueTask<long> IncrementAsync(
        string key,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        IncrementCallCount++;
        return ValueTask.FromResult(Increment(key, expiresWhen));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ExpiringCounterCount>> IncrementUntilExceededAsync(
        IReadOnlyList<ExpiringCounterIncrement> increments,
        CancellationToken cancellationToken
    )
    {
        IncrementCallCount++;
        var counts = new List<ExpiringCounterCount>();
        foreach (var increment in increments)
        {
            var count = Increment(increment.Key, increment.ExpiresWhen);
            var reference =
                increment.ReferenceKey is { } referenceKey && increment.ReferenceWeight > 0
                    ? Read(referenceKey)
                    : 0;
            counts.Add(new ExpiringCounterCount(count, reference));
            if (count + increment.ReferenceWeight * reference > increment.Limit)
                break;
        }

        return ValueTask.FromResult<IReadOnlyList<ExpiringCounterCount>>(counts);
    }

    /// <inheritdoc />
    public ValueTask<long> GetAsync(string key, CancellationToken cancellationToken)
    {
        ReadCount++;
        return ValueTask.FromResult(Read(key));
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        Counters.Remove(key);
        return ValueTask.CompletedTask;
    }

    private long Read(string key) =>
        Counters.TryGetValue(key, out var counter) && counter.ExpiresWhen > timeProvider.GetUtcNow()
            ? counter.Count
            : 0;

    private long Increment(string key, DateTimeOffset expiresWhen)
    {
        var now = timeProvider.GetUtcNow();
        if (expiresWhen <= now)
            return 0;

        var count =
            Counters.TryGetValue(key, out var counter) && counter.ExpiresWhen > now
                ? counter.Count + 1
                : 1;
        var expiry = count == 1 ? expiresWhen : counter.ExpiresWhen;
        Counters[key] = (count, expiry);
        return count;
    }
}
