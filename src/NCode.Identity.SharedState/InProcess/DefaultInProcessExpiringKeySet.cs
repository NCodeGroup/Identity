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
/// Provides the in-process implementation of <see cref="IExpiringKeySet"/>: a concurrent dictionary of keys to their
/// expiry, so add-if-absent is atomic among the threads of one process. Expired keys are treated as absent on every
/// read and are removed by a sweep that runs at most once per <see cref="SweepInterval"/>, on the next add after it
/// falls due, so memory is bounded by the keys that are live within one interval.
/// </summary>
internal class DefaultInProcessExpiringKeySet(TimeProvider timeProvider) : IExpiringKeySet
{
    internal static TimeSpan SweepInterval => TimeSpan.FromMinutes(1);

    // The UTC ticks at which the next sweep falls due; an Interlocked target, so it is a field.
    private long _nextSweepTicks;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ConcurrentDictionary<string, DateTimeOffset> Entries { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of entries held, including expired entries that have not yet been swept.
    /// </summary>
    internal int Count => Entries.Count;

    /// <inheritdoc />
    public ValueTask<bool> TryAddAsync(
        string key,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();
        if (expiresWhen <= now)
            return ValueTask.FromResult(false);

        SweepIfDue(now);

        return ValueTask.FromResult(TryAdd(key, expiresWhen, now));
    }

    /// <inheritdoc />
    public ValueTask<bool> ContainsAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();
        var isPresent = Entries.TryGetValue(key, out var expiresWhen) && expiresWhen > now;

        return ValueTask.FromResult(isPresent);
    }

    private bool TryAdd(string key, DateTimeOffset expiresWhen, DateTimeOffset now)
    {
        while (true)
        {
            if (Entries.TryAdd(key, expiresWhen))
                return true;

            // Removed by a concurrent sweep between the two calls: retry the add.
            if (!Entries.TryGetValue(key, out var existing))
                continue;

            if (existing > now)
                return false;

            // An expired entry is absent; replace it only if no other thread replaced it first, so exactly one wins.
            if (Entries.TryUpdate(key, expiresWhen, existing))
                return true;
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
            // Removes the pair only if it still holds this expiry, so a concurrent re-add is never lost.
            if (entry.Value <= now)
                Entries.TryRemove(entry);
        }
    }
}
