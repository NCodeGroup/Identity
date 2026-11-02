// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
/// and once more on shutdown. Records are written in batches of up to <see cref="BatchSize"/>, each its own unit of
/// work. A batch that cannot be written is held and written first by the next flush; the store skips the records it
/// already holds, so a batch whose save committed but reported a failure is not written twice.
/// </summary>
// See ADR-1303.
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

    // The records in Batch, kept with Interlocked so a stop whose token ended the wait for FlushGate can count them
    // without reading the list.
    private int _heldRecordCount;

    // Serializes the periodic and the final flush, which share the held batch. Never disposed: it allocates no wait
    // handle, and a stop that races the host's disposal must still release it.
    private SemaphoreSlim FlushGate { get; } = new(initialCount: 1, maxCount: 1);

    private List<PersistedAuditRecord> Batch { get; } = new(BatchSize);

    private Lock StopGate { get; } = new();

    private Task? StopTask { get; set; }

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
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown; StopAsync writes what is left.
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

        if (!Options.Enabled)
            return;

        try
        {
            await FlushGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The stop token ended the wait, so the flush that holds the gate owns the batch and cannot be read
            // safely; the held records are counted through the Interlocked field.
            LogDiscardedRecords();
            return;
        }

        try
        {
            try
            {
                await FlushHeldAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The host stopped waiting; what is left is counted below.
            }

            LogDiscardedRecords();
        }
        finally
        {
            FlushGate.Release();
        }
    }

    /// <summary>
    /// Logs the records that this replica holds and cannot write to the audit store before it stops.
    /// </summary>
    private void LogDiscardedRecords()
    {
        var discarded = Volatile.Read(ref _heldRecordCount) + Queue.Count;
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
            await FlushHeldAsync(cancellationToken);
        }
        finally
        {
            FlushGate.Release();
        }
    }

    // Runs under FlushGate, which the periodic flush and the final flush each hold.
    private async ValueTask FlushHeldAsync(CancellationToken cancellationToken)
    {
        var dropped = Queue.TakeDroppedCount();
        if (dropped > 0)
            Logger.AuditRecordsDropped(dropped, Options.QueueCapacity);

        while (true)
        {
            Queue.Drain(Batch, BatchSize);
            Interlocked.Exchange(ref _heldRecordCount, Batch.Count);
            if (Batch.Count == 0)
                return;

            if (!await TryWriteAsync(cancellationToken))
                return;

            Batch.Clear();
            Interlocked.Exchange(ref _heldRecordCount, Batch.Count);
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
}
