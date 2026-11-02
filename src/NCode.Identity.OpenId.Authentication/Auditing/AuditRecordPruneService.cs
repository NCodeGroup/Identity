// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Removes the audit records older than <see cref="AuditRecordOptions.Retention"/> every
/// <see cref="AuditRecordOptions.PruneInterval"/>, in batches of <see cref="BatchSize"/> records, each its own unit of
/// work, repeating while a batch is full. A prune that fails is logged and run again at the next interval.
/// </summary>
// See ADR-1303.
internal sealed class AuditRecordPruneService(
    IStoreManagerFactory storeManagerFactory,
    IOptions<AuditRecordOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditRecordPruneService> logger
) : BackgroundService
{
    /// <summary>
    /// The number of audit records removed in one unit of work.
    /// </summary>
    internal const int BatchSize = 256;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private AuditRecordOptions Options { get; } = options.Value;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<AuditRecordPruneService> Logger { get; } = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.Enabled)
            return;

        using var timer = new PeriodicTimer(Options.PruneInterval, TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PruneAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>
    /// Removes the audit records older than <see cref="AuditRecordOptions.Retention"/> in batches of
    /// <see cref="BatchSize"/> records, each its own unit of work, while a batch is full.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask PruneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = TimeProvider.GetUtcNow() - Options.Retention;
            while (await RemoveBatchBeforeAsync(cutoff, cancellationToken) == BatchSize) { }
        }
        // A timeout throws OperationCanceledException with the token untouched; only a cancelled token is shutdown.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.AuditRecordPruneFailed(exception);
        }
    }

    /// <summary>
    /// Removes one batch of the audit records older than <paramref name="cutoff"/> in its own unit of work.
    /// </summary>
    /// <param name="cutoff">The earliest event time to keep.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of records removed.</returns>
    private async ValueTask<int> RemoveBatchBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IAuditRecordStore>();
        var removed = await store.RemoveBeforeAsync(cutoff, BatchSize, cancellationToken);

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another replica removed records of this batch first, which is progress, so read the next batch.
            return BatchSize;
        }

        return removed;
    }
}
