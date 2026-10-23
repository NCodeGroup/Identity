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

using System.Collections.Frozen;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Removes the grants that ended before <see cref="GrantRetentionOptions.Retention"/> every
/// <see cref="GrantRetentionOptions.PruneInterval"/>, in batches of <see cref="BatchSize"/> grants, each its own unit of
/// work, repeating while a batch is full. A prune that fails is logged and run again at the next interval.
/// </summary>
// See ADR-0218.
internal sealed class GrantPruneService(
    IStoreManagerFactory storeManagerFactory,
    IOptions<GrantRetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<GrantPruneService> logger
) : BackgroundService
{
    /// <summary>
    /// The number of grants removed in one unit of work.
    /// </summary>
    internal const int BatchSize = 256;

    /// <summary>
    /// Gets the types of the grants that end only when they are revoked, so an expired grant of such a type is kept
    /// until it is revoked.
    /// </summary>
    // See ADR-0223.
    internal static FrozenSet<string> GrantTypesEndedByRevocation { get; } =
        FrozenSet.ToFrozenSet(
            [OpenIdConstants.PersistedGrantTypes.Session],
            StringComparer.Ordinal
        );

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private GrantRetentionOptions Options { get; } = options.Value;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<GrantPruneService> Logger { get; } = logger;

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
    /// Removes the grants that ended before <see cref="GrantRetentionOptions.Retention"/> in batches of
    /// <see cref="BatchSize"/> grants, each its own unit of work, while a batch is full.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask PruneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var endedBefore = TimeProvider.GetUtcNow() - Options.Retention;
            while (await RemoveBatchAsync(endedBefore, cancellationToken) == BatchSize) { }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.GrantPruneFailed(exception);
        }
    }

    /// <summary>
    /// Removes one batch of the grants that ended before <paramref name="endedBefore"/> in its own unit of work.
    /// </summary>
    /// <param name="endedBefore">The time before which a grant must have ended to be removed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of grants removed.</returns>
    private async ValueTask<int> RemoveBatchAsync(
        DateTimeOffset endedBefore,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();
        var removed = await store.RemoveEndedBeforeAsync(
            endedBefore,
            GrantTypesEndedByRevocation,
            BatchSize,
            cancellationToken
        );

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another replica removed grants of this batch first, which is progress, so read the next batch.
            return BatchSize;
        }

        return removed;
    }
}
