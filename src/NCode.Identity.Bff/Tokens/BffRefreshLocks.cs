// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Reference-counted locks, one per purpose and key, that serialize the token work on one key inside one replica.
/// </summary>
// One registered table serves every token service, each in the key space its purpose names: the exchange service
// holds its lock on a session while the user token service takes its own lock on the same session to refresh the
// subject token, so the two must never share a key.
internal sealed class BffRefreshLocks
{
    /// <summary>
    /// The purpose of the user token service's locks, keyed by the session's store key.
    /// </summary>
    public const string UserPurpose = "user";

    /// <summary>
    /// The purpose of the client token service's locks, keyed by the resource and scope.
    /// </summary>
    public const string ClientPurpose = "client";

    /// <summary>
    /// The purpose of the exchange token service's locks, keyed by the session's store key.
    /// </summary>
    public const string ExchangePurpose = "exchange";

    private Lock Gate { get; } = new();

    private Dictionary<(string Purpose, string Key), Entry> Entries { get; } = [];

    /// <summary>
    /// Gets the number of locks that are currently held or awaited.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (Gate)
            {
                return Entries.Count;
            }
        }
    }

    /// <summary>
    /// Waits to enter the lock of the specified key, in the key space of the specified purpose.
    /// </summary>
    /// <param name="purpose">The purpose that names the key space, such as <see cref="UserPurpose"/>.</param>
    /// <param name="key">The key to lock, such as the store key of a session.</param>
    /// <param name="timeout">The longest time to wait for the lock.</param>
    /// <param name="cancellationToken">The token to observe while waiting.</param>
    /// <returns>A lease that releases the lock when disposed, or <c>null</c> when the wait timed out.</returns>
    public async ValueTask<IDisposable?> TryEnterAsync(
        string purpose,
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        var entryKey = (purpose, key);
        var entry = Acquire(entryKey);

        var entered = false;
        try
        {
            entered = await entry.Semaphore.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            if (!entered)
                Leave(entryKey, entry);
        }

        return entered ? new Releaser(this, entryKey, entry) : null;
    }

    private Entry Acquire((string Purpose, string Key) entryKey)
    {
        lock (Gate)
        {
            if (!Entries.TryGetValue(entryKey, out var entry))
            {
                entry = new Entry();
                Entries.Add(entryKey, entry);
            }

            entry.References++;
            return entry;
        }
    }

    private void Leave((string Purpose, string Key) entryKey, Entry entry)
    {
        lock (Gate)
        {
            entry.References--;
            if (entry.References > 0)
                return;

            Entries.Remove(entryKey);
            entry.Semaphore.Dispose();
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int References { get; set; }
    }

    private sealed class Releaser(
        BffRefreshLocks owner,
        (string Purpose, string Key) entryKey,
        Entry entry
    ) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return;

            entry.Semaphore.Release();
            owner.Leave(entryKey, entry);
        }
    }
}
