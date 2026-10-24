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

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Adds the usage each replica recorded to the durable daily ledger every <see cref="TenantUsageOptions.FlushInterval"/>
/// and once more on shutdown, and removes the days the ledger no longer keeps. Usage is added in numbered batches of up
/// to <see cref="BatchSize"/> entries, each its own unit of work. A batch that cannot be added — the database is
/// unreachable, or another replica updated one of its entries first — is held and retried under its number before any
/// new usage is drained, so each batch is added to the ledger once.
/// </summary>
// See ADR-0199 and ADR-0209.
internal sealed class TenantUsageFlushService(
    DefaultTenantUsageRecorder recorder,
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantUsageOptions> options,
    TimeProvider timeProvider,
    ILogger<TenantUsageFlushService> logger
) : BackgroundService
{
    /// <summary>
    /// The number of ledger entries added or removed in one unit of work.
    /// </summary>
    internal const int BatchSize = 256;

    private DateOnly? _lastPrunedDay;

    // The entries in PendingBatches, kept with Interlocked so a stop whose token ended the wait for FlushGate can count
    // them without reading the queue.
    private int _heldEntryCount;

    /// <summary>
    /// Gets the identifier this replica adds its batches under, drawn when the service is created.
    /// </summary>
    internal Guid ReplicaId { get; } = Guid.NewGuid();

    // Serializes the periodic and the final flush, which share the pending batches. Never disposed: it allocates no
    // wait handle, and a stop that races the host's disposal must still release it.
    private SemaphoreSlim FlushGate { get; } = new(initialCount: 1, maxCount: 1);

    private Queue<TenantUsageBatch> PendingBatches { get; } = new();

    private long LastBatchNumber { get; set; }

    private Lock StopGate { get; } = new();

    private Task? StopTask { get; set; }

    private DefaultTenantUsageRecorder Recorder { get; } = recorder;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private TenantUsageOptions Options { get; } = options.Value;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<TenantUsageFlushService> Logger { get; } = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            // Expected on shutdown; StopAsync adds what is left.
        }
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // A host can stop a service twice at once, and every caller must wait for the one final flush, or its owner
        // disposes the services under it.
        lock (StopGate)
        {
            return StopTask ??= StopOnceAsync(cancellationToken);
        }
    }

    private async Task StopOnceAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        try
        {
            await FlushGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The stop token ended the wait, so the flush that holds the gate owns the batches and cannot be read
            // safely; the held entries are counted through the Interlocked field.
            LogDiscardedUsage();
            return;
        }

        try
        {
            await AddUsageAsync(cancellationToken);
            LogDiscardedUsage();
        }
        finally
        {
            FlushGate.Release();
        }
    }

    /// <summary>
    /// Logs the usage that this replica holds and cannot add to the ledger before it stops.
    /// </summary>
    private void LogDiscardedUsage()
    {
        var discarded = Volatile.Read(ref _heldEntryCount) + Recorder.Drain().Count;
        if (discarded > 0)
            Logger.TenantUsageDiscardedOnStop(discarded);
    }

    /// <summary>
    /// Adds the batches held by earlier flushes, and then the usage recorded since, to the ledger, one batch per unit
    /// of work.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        await FlushGate.WaitAsync(cancellationToken);
        try
        {
            await AddUsageAsync(cancellationToken);
        }
        finally
        {
            FlushGate.Release();
        }
    }

    private async ValueTask AddUsageAsync(CancellationToken cancellationToken)
    {
        // A held batch is retried, in order, before new usage is drained, because the ledger skips a batch numbered at
        // or below the last one added.
        if (!await AddPendingBatchesAsync(cancellationToken))
            return;

        EnqueueRecordedUsage();
        await AddPendingBatchesAsync(cancellationToken);
    }

    private void EnqueueRecordedUsage()
    {
        // Ordered so a batch spans few days and its reads stay narrow.
        var chunks = Recorder
            .Drain()
            .OrderBy(pair => pair.Key.Day)
            .ThenBy(pair => pair.Key.TenantId, StringComparer.Ordinal)
            .Chunk(BatchSize);

        foreach (var chunk in chunks)
        {
            var batch = new TenantUsageBatch(
                ++LastBatchNumber,
                [
                    .. chunk.Select(pair => new PersistedTenantUsage
                    {
                        TenantId = pair.Key.TenantId,
                        Day = pair.Key.Day,
                        Meter = pair.Key.Meter,
                        Count = pair.Value,
                    }),
                ]
            );

            PendingBatches.Enqueue(batch);
            Interlocked.Add(ref _heldEntryCount, batch.Usage.Length);
        }
    }

    private async ValueTask<bool> AddPendingBatchesAsync(CancellationToken cancellationToken)
    {
        while (PendingBatches.TryPeek(out var batch))
        {
            if (!await TryAddAsync(batch, cancellationToken))
                return false;

            PendingBatches.Dequeue();
            Interlocked.Add(ref _heldEntryCount, -batch.Usage.Length);
        }

        return true;
    }

    private async ValueTask<bool> TryAddAsync(
        TenantUsageBatch batch,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<ITenantUsageStore>();
            await store.AddAsync(ReplicaId, batch.Number, batch.Usage, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            if (!cancellationToken.IsCancellationRequested)
                Logger.TenantUsageFlushFailed(batch.Usage.Length, exception);

            return false;
        }
    }

    /// <summary>
    /// Removes the days older than <see cref="TenantUsageOptions.Retention"/> in batches of <see cref="BatchSize"/>
    /// entries, each its own unit of work, once per UTC calendar day.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask PruneAsync(CancellationToken cancellationToken)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        if (_lastPrunedDay == today)
            return;

        try
        {
            var firstKeptDay = DateOnly.FromDateTime(now - Options.Retention);
            while (await RemoveBatchBeforeAsync(firstKeptDay, cancellationToken) == BatchSize) { }

            _lastPrunedDay = today;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TenantUsagePruneFailed(exception);
        }
    }

    /// <summary>
    /// Removes one batch of the ledger's entries counted before <paramref name="firstKeptDay"/> in its own unit of work.
    /// </summary>
    /// <param name="firstKeptDay">The first UTC calendar day to keep.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of entries removed.</returns>
    private async ValueTask<int> RemoveBatchBeforeAsync(
        DateOnly firstKeptDay,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantUsageStore>();
        var removed = await store.RemoveBeforeAsync(firstKeptDay, BatchSize, cancellationToken);

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another replica removed rows of this batch first, which is progress, so read the next batch. See ADR-0213.
            return BatchSize;
        }

        return removed;
    }
}
