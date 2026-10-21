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
/// and once more on shutdown, and removes the days the ledger no longer keeps (ADR-0199). Usage is added in batches of
/// <see cref="BatchSize"/> entries, each its own unit of work, so a conflict holds back only its batch. A batch that
/// cannot be added — the database is unreachable, or another replica updated one of its entries first — is returned to
/// the recorder and added by the next flush.
/// </summary>
internal sealed class TenantUsageFlushService(
    DefaultTenantUsageRecorder recorder,
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantUsageOptions> options,
    TimeProvider timeProvider,
    ILogger<TenantUsageFlushService> logger
) : BackgroundService
{
    /// <summary>
    /// The number of ledger entries added in one unit of work.
    /// </summary>
    internal const int BatchSize = 256;

    private DateOnly? _lastPrunedDay;

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
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the usage recorded since the last flush to the ledger, one batch per unit of work.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        // Ordered so a batch spans few days and its reads stay narrow.
        var batches = Recorder
            .Drain()
            .OrderBy(pair => pair.Key.Day)
            .ThenBy(pair => pair.Key.TenantId, StringComparer.Ordinal)
            .Chunk(BatchSize);

        foreach (var batch in batches)
        {
            try
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ITenantUsageStore>();
                var usage = batch
                    .Select(pair => new PersistedTenantUsage
                    {
                        TenantId = pair.Key.TenantId,
                        Day = pair.Key.Day,
                        Meter = pair.Key.Meter,
                        Count = pair.Value,
                    })
                    .ToList();
                await store.AddAsync(usage, cancellationToken);
                await storeManager.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                Recorder.Restore(batch);
                Logger.TenantUsageFlushFailed(batch.Length, exception);
            }
        }
    }

    /// <summary>
    /// Removes the days older than <see cref="TenantUsageOptions.Retention"/>, once per UTC calendar day.
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
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<ITenantUsageStore>();
            await store.RemoveBeforeAsync(firstKeptDay, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
            _lastPrunedDay = today;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TenantUsagePruneFailed(exception);
        }
    }
}
