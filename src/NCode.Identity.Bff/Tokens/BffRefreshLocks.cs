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

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Reference-counted locks, one per session store key, that serialize the token refreshes of one session inside one
/// replica.
/// </summary>
internal sealed class BffRefreshLocks
{
    private Lock Gate { get; } = new();

    private Dictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the number of session locks that are currently held or awaited.
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
    /// Waits to enter the lock of the specified session.
    /// </summary>
    /// <param name="storeKey">The store key of the session.</param>
    /// <param name="timeout">The longest time to wait for the lock.</param>
    /// <param name="cancellationToken">The token to observe while waiting.</param>
    /// <returns>A lease that releases the lock when disposed, or <c>null</c> when the wait timed out.</returns>
    public async ValueTask<IDisposable?> TryEnterAsync(
        string storeKey,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        var entry = Acquire(storeKey);

        var entered = false;
        try
        {
            entered = await entry.Semaphore.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            if (!entered)
                Leave(storeKey, entry);
        }

        return entered ? new Releaser(this, storeKey, entry) : null;
    }

    private Entry Acquire(string storeKey)
    {
        lock (Gate)
        {
            if (!Entries.TryGetValue(storeKey, out var entry))
            {
                entry = new Entry();
                Entries.Add(storeKey, entry);
            }

            entry.References++;
            return entry;
        }
    }

    private void Leave(string storeKey, Entry entry)
    {
        lock (Gate)
        {
            entry.References--;
            if (entry.References > 0)
                return;

            Entries.Remove(storeKey);
            entry.Semaphore.Dispose();
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int References { get; set; }
    }

    private sealed class Releaser(BffRefreshLocks owner, string storeKey, Entry entry) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return;

            entry.Semaphore.Release();
            owner.Leave(storeKey, entry);
        }
    }
}
