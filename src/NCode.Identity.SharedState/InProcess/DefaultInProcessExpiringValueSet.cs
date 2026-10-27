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
/// Provides the in-process implementation of <see cref="IExpiringValueSet"/>: a concurrent dictionary of keys to
/// immutable versioned entries, each changed by compare-and-swap, so an add, replace or take is atomic among the threads
/// of one process. An expired value is treated as absent on every read and is replaced by the next add; expired values
/// are removed by a sweep that runs at most once per <see cref="SweepInterval"/>, on the next add or replace after it
/// falls due, so memory is bounded by the values that are live within one interval.
/// </summary>
internal class DefaultInProcessExpiringValueSet(TimeProvider timeProvider) : IExpiringValueSet
{
    internal static TimeSpan SweepInterval => TimeSpan.FromMinutes(1);

    // The UTC ticks at which the next sweep falls due; an Interlocked target, so it is a field.
    private long _nextSweepTicks;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ConcurrentDictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of values held, including expired values that have not yet been swept.
    /// </summary>
    internal int Count => Entries.Count;

    /// <inheritdoc />
    public ValueTask<bool> TryAddAsync(
        string key,
        ReadOnlyMemory<byte> value,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();
        if (expiresWhen <= now)
            return ValueTask.FromResult(false);

        SweepIfDue(now);

        var entry = new Entry(value.ToArray(), 1, expiresWhen);
        while (true)
        {
            if (Entries.TryAdd(key, entry))
                return ValueTask.FromResult(true);

            // Removed by a concurrent sweep or take between the two calls: retry the add.
            if (!Entries.TryGetValue(key, out var existing))
                continue;

            if (existing.ExpiresWhen > now)
                return ValueTask.FromResult(false);

            // An expired value is absent; replace it only if no other thread replaced it first.
            if (Entries.TryUpdate(key, entry, existing))
                return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask<ExpiringValue?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();

        ExpiringValue? result =
            Entries.TryGetValue(key, out var entry) && entry.ExpiresWhen > now
                ? new ExpiringValue(entry.Value, entry.Version)
                : null;

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryReplaceAsync(
        string key,
        long expectedVersion,
        ReadOnlyMemory<byte> value,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();
        if (expiresWhen <= now)
            return ValueTask.FromResult(false);

        SweepIfDue(now);

        if (
            !Entries.TryGetValue(key, out var existing)
            || existing.ExpiresWhen <= now
            || existing.Version != expectedVersion
        )
            return ValueTask.FromResult(false);

        var replacement = existing with
        {
            Value = value.ToArray(),
            Version = expectedVersion + 1,
            ExpiresWhen = expiresWhen,
        };

        return ValueTask.FromResult(Entries.TryUpdate(key, replacement, existing));
    }

    /// <inheritdoc />
    public ValueTask<ExpiringValue?> TryTakeAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = TimeProvider.GetUtcNow();

        while (true)
        {
            if (!Entries.TryGetValue(key, out var entry))
                return ValueTask.FromResult<ExpiringValue?>(null);

            // Removes the pair only if it still holds this entry, so exactly one caller takes it.
            var removed = Entries.TryRemove(KeyValuePair.Create(key, entry));

            if (entry.ExpiresWhen <= now)
                return ValueTask.FromResult<ExpiringValue?>(null);

            if (removed)
                return ValueTask.FromResult<ExpiringValue?>(
                    new ExpiringValue(entry.Value, entry.Version)
                );
        }
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        Entries.TryRemove(key, out _);

        return ValueTask.CompletedTask;
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
            // Removes the pair only if it still holds this entry, so a concurrent replacement is never lost.
            if (entry.Value.ExpiresWhen <= now)
                Entries.TryRemove(entry);
        }
    }

    private sealed record Entry(byte[] Value, long Version, DateTimeOffset ExpiresWhen);
}
