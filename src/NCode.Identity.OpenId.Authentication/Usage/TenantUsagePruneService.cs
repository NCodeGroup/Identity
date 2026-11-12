// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Removes the days of the usage ledger older than <see cref="TenantUsageOptions.Retention"/> every
/// <see cref="TenantUsageOptions.PruneInterval"/>, in batches of <see cref="BatchSize"/> entries, each its own unit of
/// work, repeating while a batch is full. A prune that fails is logged and run again at the next interval.
/// </summary>
// See ADR-1199.
internal sealed class TenantUsagePruneService(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantUsageOptions> options,
    TimeProvider timeProvider,
    ILogger<TenantUsagePruneService> logger
) : BackgroundService
{
    /// <summary>
    /// The number of ledger entries removed in one unit of work.
    /// </summary>
    internal const int BatchSize = 256;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private TenantUsageOptions Options { get; } = options.Value;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<TenantUsagePruneService> Logger { get; } = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
    /// Removes the days older than <see cref="TenantUsageOptions.Retention"/> in batches of <see cref="BatchSize"/>
    /// entries, each its own unit of work, while a batch is full.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask PruneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var firstKeptDay = DateOnly.FromDateTime(
                (TimeProvider.GetUtcNow() - Options.Retention).UtcDateTime
            );
            while (await RemoveBatchBeforeAsync(firstKeptDay, cancellationToken) == BatchSize) { }
        }
        // A timeout throws OperationCanceledException with the token untouched; only a cancelled token is shutdown.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
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
            // Another replica removed rows of this batch first, which is progress, so read the next batch. See ADR-1199.
            return BatchSize;
        }

        return removed;
    }
}
