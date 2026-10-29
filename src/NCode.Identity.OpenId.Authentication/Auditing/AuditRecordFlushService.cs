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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Writes the audit records each replica holds to the audit store every <see cref="AuditRecordOptions.FlushInterval"/>
/// and once more on shutdown, and removes the records older than <see cref="AuditRecordOptions.Retention"/> once an hour.
/// Records are written in batches of up to <see cref="BatchSize"/>, each its own unit of work. A batch that cannot be
/// written is held and written first by the next flush; the store skips the records it already holds, so a batch whose
/// save committed but reported a failure is not written twice.
/// </summary>
// See ADR-0303.
internal sealed class AuditRecordFlushService(
    AuditRecordQueue queue,
    IStoreManagerFactory storeManagerFactory,
    IOptions<AuditRecordOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditRecordFlushService> logger
) : BackgroundService
{
    /// <summary>
    /// The number of audit records written or removed in one unit of work.
    /// </summary>
    internal const int BatchSize = 256;

    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(1);

    private DateTimeOffset? _lastPrunedTime;

    // Serializes the periodic and the final flush, which share the held batch. Never disposed: it allocates no wait
    // handle, and a stop that races the host's disposal must still release it.
    private SemaphoreSlim FlushGate { get; } = new(initialCount: 1, maxCount: 1);

    private List<PersistedAuditRecord> Batch { get; } = new(BatchSize);

    private AuditRecordQueue Queue { get; } = queue;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private AuditRecordOptions Options { get; } = options.Value;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<AuditRecordFlushService> Logger { get; } = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.Enabled)
            return;

        using var timer = new PeriodicTimer(Options.FlushInterval, TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAsync(stoppingToken);
                await PruneAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown; StopAsync writes what is left.
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (!Options.Enabled)
            return;

        try
        {
            await FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The host stopped waiting; what is left is counted below.
        }

        var discarded = Batch.Count + Queue.Count;
        if (discarded > 0)
            Logger.AuditRecordsDiscardedOnStop(discarded);
    }

    /// <summary>
    /// Writes the held batch, and then the records queued since, to the store, one batch per unit of work, until the
    /// queue is empty or a batch cannot be written.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        await FlushGate.WaitAsync(cancellationToken);
        try
        {
            var dropped = Queue.TakeDroppedCount();
            if (dropped > 0)
                Logger.AuditRecordsDropped(dropped, Options.QueueCapacity);

            while (true)
            {
                Queue.Drain(Batch, BatchSize);
                if (Batch.Count == 0)
                    return;

                if (!await TryWriteAsync(cancellationToken))
                    return;

                Batch.Clear();
            }
        }
        finally
        {
            FlushGate.Release();
        }
    }

    private async ValueTask<bool> TryWriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IAuditRecordStore>();
            await store.AddAsync(Batch, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.AuditRecordFlushFailed(Batch.Count, exception);
            return false;
        }
    }

    /// <summary>
    /// Removes the records older than <see cref="AuditRecordOptions.Retention"/> in batches of <see cref="BatchSize"/>,
    /// each its own unit of work, at most once per hour.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask PruneAsync(CancellationToken cancellationToken)
    {
        var now = TimeProvider.GetUtcNow();
        if (_lastPrunedTime is { } lastPrunedTime && now - lastPrunedTime < PruneInterval)
            return;

        var cutoff = now - Options.Retention;
        try
        {
            int removed;
            do
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IAuditRecordStore>();
                removed = await store.RemoveBeforeAsync(cutoff, BatchSize, cancellationToken);
                await storeManager.SaveChangesAsync(cancellationToken);
            } while (removed == BatchSize);

            _lastPrunedTime = now;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Another replica that removed the same records first, or a store that is unreachable, is retried by the next
            // flush.
            Logger.AuditRecordPruneFailed(exception);
        }
    }
}
