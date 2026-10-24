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

namespace NCode.Identity.SharedState.InProcess;

/// <summary>
/// Provides the in-process implementation of <see cref="IExpiringCounterSet"/>: a concurrent dictionary of keys to
/// counters, each incremented with <see cref="Interlocked"/>, so an increment is atomic among the threads of one
/// process. An expired counter is treated as absent on every read and is replaced by the next increment; expired
/// counters are removed by a sweep that runs at most once per <see cref="SweepInterval"/>, on the next increment after
/// it falls due, so memory is bounded by the counters that are live within one interval.
/// </summary>
internal class DefaultInProcessExpiringCounterSet(TimeProvider timeProvider) : IExpiringCounterSet
{
    internal static TimeSpan SweepInterval => TimeSpan.FromMinutes(1);

    // The UTC ticks at which the next sweep falls due; an Interlocked target, so it is a field.
    private long _nextSweepTicks;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ConcurrentDictionary<string, Counter> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of counters held, including expired counters that have not yet been swept.
    /// </summary>
    internal int Count => Entries.Count;

    /// <inheritdoc />
    public ValueTask<long> IncrementAsync(
        string key,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();
        if (expiresWhen <= now)
            return ValueTask.FromResult(0L);

        SweepIfDue(now);

        return ValueTask.FromResult(Increment(key, expiresWhen, now));
    }

    /// <inheritdoc />
    public ValueTask<long> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();
        var count =
            Entries.TryGetValue(key, out var counter) && counter.ExpiresWhen > now
                ? counter.Read()
                : 0L;

        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        Entries.TryRemove(key, out _);

        return ValueTask.CompletedTask;
    }

    private long Increment(string key, DateTimeOffset expiresWhen, DateTimeOffset now)
    {
        while (true)
        {
            var started = new Counter(expiresWhen);
            if (Entries.TryAdd(key, started))
                return started.Increment();

            // Removed by a concurrent sweep between the two calls: retry the add.
            if (!Entries.TryGetValue(key, out var existing))
                continue;

            if (existing.ExpiresWhen > now)
                return existing.Increment();

            // An expired counter is absent; replace it only if no other thread replaced it first, so every increment
            // lands on the one counter that survives.
            if (Entries.TryUpdate(key, started, existing))
                return started.Increment();
        }
    }

    private void SweepIfDue(DateTimeOffset now)
    {
        var nextSweepTicks = Interlocked.Read(ref _nextSweepTicks);
        if (now.UtcTicks < nextSweepTicks)
            return;

        var claimed = Interlocked.CompareExchange(
            ref _nextSweepTicks,
            (now + SweepInterval).UtcTicks,
            nextSweepTicks
        );
        if (claimed != nextSweepTicks)
            return;

        foreach (var entry in Entries)
        {
            // Removes the pair only if it still holds this counter, so a concurrent replacement is never lost.
            if (entry.Value.ExpiresWhen <= now)
                Entries.TryRemove(entry);
        }
    }

    private sealed class Counter(DateTimeOffset expiresWhen)
    {
        // An Interlocked target, so it is a field.
        private long _count;

        public DateTimeOffset ExpiresWhen { get; } = expiresWhen;

        public long Increment() => Interlocked.Increment(ref _count);

        public long Read() => Interlocked.Read(ref _count);
    }
}
