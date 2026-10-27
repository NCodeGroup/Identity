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
/// Provides the in-process implementation of <see cref="IExpiringIndexSet"/>: a concurrent dictionary of index keys to
/// indexes, each holding its members and their expiries under its own lock, so an add, remove or read is atomic among
/// the threads of one process. An expired member is treated as absent on every read; expired members are removed by a
/// sweep that runs at most once per <see cref="SweepInterval"/>, on the next add after it falls due, so memory is
/// bounded by the members that are live within one interval.
/// </summary>
internal class DefaultInProcessExpiringIndexSet(TimeProvider timeProvider) : IExpiringIndexSet
{
    internal static TimeSpan SweepInterval => TimeSpan.FromMinutes(1);

    // The UTC ticks at which the next sweep falls due; an Interlocked target, so it is a field.
    private long _nextSweepTicks;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ConcurrentDictionary<string, Index> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of indexes held, including indexes whose members have all expired but have not yet been swept.
    /// </summary>
    internal int Count => Entries.Count;

    /// <inheritdoc />
    public ValueTask AddMemberAsync(
        string indexKey,
        string member,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(indexKey);
        ArgumentNullException.ThrowIfNull(member);

        var now = TimeProvider.GetUtcNow();
        if (expiresWhen <= now)
            return ValueTask.CompletedTask;

        SweepIfDue(now);

        while (true)
        {
            var index = Entries.GetOrAdd(indexKey, _ => new Index());
            lock (index.Sync)
            {
                // A removal that raced this add discarded the index: retry on a fresh one instead of writing into it.
                if (index.Removed)
                    continue;

                index.Members[member] = expiresWhen;
                break;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetMembersAsync(
        string indexKey,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(indexKey);

        var now = TimeProvider.GetUtcNow();

        if (!Entries.TryGetValue(indexKey, out var index))
            return ValueTask.FromResult<IReadOnlyList<string>>([]);

        string[] members;
        lock (index.Sync)
        {
            members = index
                .Members.Where(pair => pair.Value > now)
                .Select(pair => pair.Key)
                .ToArray();
        }

        return ValueTask.FromResult<IReadOnlyList<string>>(members);
    }

    /// <inheritdoc />
    public ValueTask RemoveMemberAsync(
        string indexKey,
        string member,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(indexKey);
        ArgumentNullException.ThrowIfNull(member);

        if (Entries.TryGetValue(indexKey, out var index))
        {
            lock (index.Sync)
            {
                index.Members.Remove(member);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string indexKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexKey);

        if (Entries.TryRemove(indexKey, out var index))
        {
            lock (index.Sync)
            {
                index.Removed = true;
            }
        }

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
            var index = entry.Value;
            lock (index.Sync)
            {
                var expired = index
                    .Members.Where(pair => pair.Value <= now)
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (var member in expired)
                    index.Members.Remove(member);

                if (index.Members.Count > 0)
                    continue;

                // Marked removed under the lock so an add racing this removal retries on a fresh index.
                index.Removed = true;
                Entries.TryRemove(entry);
            }
        }
    }

    private sealed class Index
    {
        public Lock Sync { get; } = new();

        public Dictionary<string, DateTimeOffset> Members { get; } = new(StringComparer.Ordinal);

        // Set, under Sync, once the index has been removed from the dictionary: an add that obtained this index
        // just before the removal sees it and retries on a fresh index instead of writing into a discarded one.
        public bool Removed { get; set; }
    }
}
