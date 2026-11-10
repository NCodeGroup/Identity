// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Caches the clients that a referenced key wrapper builds to reach a key through an access identity, and runs each
/// operation with such a client.
/// </summary>
/// <typeparam name="TClient">The type of the client; only a client that is <see cref="IDisposable"/> is disposed.</typeparam>
/// <remarks>
/// <para>
/// The access identities come from requests of the owners of the keys, so the cache keeps a client only while it
/// works. An entry is confirmed by its first successful operation and is then kept until it is idle or the cache is
/// disposed. An operation that fails on an entry that is not confirmed removes the entry, so an identity that does not
/// work leaves nothing cached. A failure of a confirmed entry, such as a throttle, and a cancellation, remove nothing.
/// </para>
/// <para>
/// An entry creates its client once, and callers that race share the creation. A creation that fails is not cached. A
/// removed entry disposes its client when the last operation in flight with it ends, so an operation never runs with a
/// disposed client. A client whose creation failed or never completed is not disposed. An entry that is not used for
/// longer than the idle timeout, with nothing in flight, is removed by the next use of the cache.
/// </para>
/// </remarks>
/// <param name="timeProvider">The provider of the timestamps that measure how long an entry is idle.</param>
/// <param name="idleTimeout">The time after which an entry that is not used is removed.</param>
// See ADR-1429.
[PublicAPI]
public sealed class KeyAccessClientCache<TClient>(TimeProvider timeProvider, TimeSpan idleTimeout)
    : IDisposable
    where TClient : class
{
    private ConcurrentDictionary<string, KeyAccessClientEntry<TClient>> Entries { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Runs an operation with the client for a key, creating and caching the client on its first use.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
    /// <param name="key">The key that identifies the client, such as one derived from an access identity.</param>
    /// <param name="create">Creates the client; it is called at most once for a cached entry.</param>
    /// <param name="operation">The operation to run with the client.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    public async ValueTask<TResult> UseAsync<TResult>(
        string key,
        Func<CancellationToken, ValueTask<TClient>> create,
        Func<TClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        RemoveIdleEntries();

        var (entry, hasEntered) = EnterEntry(key, create);
        while (!hasEntered)
        {
            Entries.TryRemove(new KeyValuePair<string, KeyAccessClientEntry<TClient>>(key, entry));
            (entry, hasEntered) = EnterEntry(key, create);
        }

        try
        {
            var clientTask = entry.Creation;

            TClient client;
            try
            {
                client = await clientTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (clientTask.IsCompleted && !clientTask.IsCompletedSuccessfully)
            {
                RemoveAndRelease(key, entry);
                throw;
            }

            TResult result;
            try
            {
                result = await operation(client, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException && !entry.IsConfirmed)
            {
                RemoveAndRelease(key, entry);
                throw;
            }

            entry.Confirm();
            return result;
        }
        finally
        {
            entry.Exit();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var pair in Entries)
        {
            if (Entries.TryRemove(pair))
                pair.Value.Release();
        }
    }

    /// <summary>
    /// Gets or adds the entry for a key and registers a use of it.
    /// </summary>
    /// <param name="key">The key that identifies the entry.</param>
    /// <param name="create">Creates the client of a new entry.</param>
    /// <returns>The entry, and whether the use is registered; a released entry does not register the use.</returns>
    private (KeyAccessClientEntry<TClient> Entry, bool HasEntered) EnterEntry(
        string key,
        Func<CancellationToken, ValueTask<TClient>> create
    )
    {
        var entry = Entries.GetOrAdd(
            key,
            static (_, state) =>
                new KeyAccessClientEntry<TClient>(state.TimeProvider, state.Create),
            (TimeProvider: timeProvider, Create: create)
        );

        return (entry, entry.Enter());
    }

    /// <summary>
    /// Removes exactly the given entry and releases it.
    /// </summary>
    /// <param name="key">The key that identifies the entry.</param>
    /// <param name="entry">The entry to remove and release.</param>
    private void RemoveAndRelease(string key, KeyAccessClientEntry<TClient> entry)
    {
        Entries.TryRemove(new KeyValuePair<string, KeyAccessClientEntry<TClient>>(key, entry));
        entry.Release();
    }

    /// <summary>
    /// Removes and releases every entry that is not used for longer than the idle timeout and has nothing in flight.
    /// </summary>
    private void RemoveIdleEntries()
    {
        foreach (var pair in Entries)
        {
            var entry = pair.Value;
            if (entry.IsInFlight || timeProvider.GetElapsedTime(entry.LastUse) <= idleTimeout)
                continue;

            if (Entries.TryRemove(pair))
                entry.Release();
        }
    }
}
