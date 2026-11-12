// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Holds one client of a <see cref="KeyAccessClientCache{TClient}"/>: the lazily created client, the uses that are in
/// flight, the instant of the last use, and whether the client is confirmed or released.
/// </summary>
/// <typeparam name="TClient">The type of the client.</typeparam>
/// <param name="timeProvider">The provider of the timestamps that record the last use.</param>
/// <param name="create">Creates the client; it is called at most once.</param>
// See ADR-1414.
internal sealed class KeyAccessClientEntry<TClient>(
    TimeProvider timeProvider,
    Func<CancellationToken, ValueTask<TClient>> create
)
    where TClient : class
{
    private int _inFlight;
    private int _confirmed;
    private int _released;
    private int _disposed;
    private long _lastUse = timeProvider.GetTimestamp();
    private TClient? _client;
    private Task<TClient>? _creation;
    private object? _creationLock;

    /// <summary>
    /// Gets a value indicating whether an operation has succeeded with the client.
    /// </summary>
    public bool IsConfirmed => Volatile.Read(ref _confirmed) != 0;

    /// <summary>
    /// Gets a value indicating whether any use is in flight.
    /// </summary>
    public bool IsInFlight => Volatile.Read(ref _inFlight) > 0;

    /// <summary>
    /// Gets the timestamp of the last time that a use entered or exited the entry.
    /// </summary>
    public long LastUse => Volatile.Read(ref _lastUse);

    /// <summary>
    /// Gets the task that creates the client on its first request and is shared by every use.
    /// </summary>
    public Task<TClient> Creation =>
        LazyInitializer.EnsureInitialized(
            ref _creation,
            ref _creationLock,
            () => CreateAsync(CancellationToken.None).AsTask()
        );

    /// <summary>
    /// Registers a use of the entry.
    /// </summary>
    /// <returns><see langword="true"/> when the use is registered; <see langword="false"/> when the entry is released,
    /// in which case the use is not registered.</returns>
    public bool Enter()
    {
        Interlocked.Increment(ref _inFlight);
        if (Volatile.Read(ref _released) == 0)
        {
            Stamp();
            return true;
        }

        Exit();
        return false;
    }

    /// <summary>
    /// Ends a use of the entry, and disposes the client when the entry is released and no other use is in flight.
    /// </summary>
    public void Exit()
    {
        Stamp();
        if (Interlocked.Decrement(ref _inFlight) == 0)
            DisposeWhenReleased();
    }

    /// <summary>
    /// Marks the entry as confirmed.
    /// </summary>
    public void Confirm() => Volatile.Write(ref _confirmed, 1);

    /// <summary>
    /// Marks the entry as released, and disposes the client at once when no use is in flight.
    /// </summary>
    public void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return;

        if (!IsInFlight)
            DisposeWhenReleased();
    }

    /// <summary>
    /// Records the current timestamp as the last use.
    /// </summary>
    private void Stamp() => Volatile.Write(ref _lastUse, timeProvider.GetTimestamp());

    /// <summary>
    /// Disposes the client once, when the entry is released, nothing is in flight and the client was created.
    /// </summary>
    private void DisposeWhenReleased()
    {
        if (Volatile.Read(ref _released) == 0 || IsInFlight)
            return;

        var client = Volatile.Read(ref _client);
        if (client is null || Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        (client as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Creates the client without a caller's cancellation, so that the shared creation is never faulted by one caller,
    /// publishes it, and disposes it at once when the entry was released while it was being created.
    /// </summary>
    /// <param name="cancellationToken">The token that is passed to the creation of the client.</param>
    /// <returns>The client.</returns>
    private async ValueTask<TClient> CreateAsync(CancellationToken cancellationToken)
    {
        var client = await create(cancellationToken).ConfigureAwait(false);

        // a full fence, so that this publication and a concurrent release can never both miss each other
        Interlocked.Exchange(ref _client, client);
        DisposeWhenReleased();
        return client;
    }
}
