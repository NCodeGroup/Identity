// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Schedules the sweep of the remote keys that no stored secret names, once the replica's platform is materialized and
/// then every <see cref="RemoteKeyOptions.SweepInterval"/>: it dispatches one <see cref="RemoteKeySweepJob"/> for each
/// described remote key provider that creates keys. One replica runs each pass, under a lease on the shared-state port
/// that lasts half the interval, and the dispatcher gives each provider's job its own retries. A provider whose
/// dispatch fails is logged and the others are dispatched.
/// </summary>
/// <remarks>
/// The service does nothing when <see cref="RemoteKeyOptions.SweepEnabled"/> is <c>false</c> or when no described
/// provider creates keys.
/// </remarks>
// See ADR-1478.
internal sealed class RemoteKeySweepService(
    IMaterializedPlatformAccessor materializedPlatformAccessor,
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog,
    IJobDispatcher jobDispatcher,
    IExpiringKeySet expiringKeySet,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<RemoteKeySweepService> logger
) : BackgroundService
{
    /// <summary>
    /// The purpose that names the lease one replica takes on each pass.
    /// </summary>
    internal const string LeasePurpose = "remote-key-sweep";

    /// <summary>
    /// The key of the lease, which the one platform of the deployment shares.
    /// </summary>
    internal const string LeaseKey = $"{LeasePurpose}:platform";

    private IMaterializedPlatformAccessor MaterializedPlatformAccessor { get; } =
        materializedPlatformAccessor;
    private IRemoteKeyDescriptorCatalog RemoteKeyDescriptorCatalog { get; } =
        remoteKeyDescriptorCatalog;
    private IJobDispatcher JobDispatcher { get; } = jobDispatcher;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;
    private IOptions<SecretProtectionOptions> Options { get; } = options;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<RemoteKeySweepService> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private RemoteKeyOptions RemoteKeys => Options.Value.RemoteKeys;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (
            !RemoteKeys.SweepEnabled
            || !RemoteKeyDescriptorCatalog.Providers.Any(provider => provider.CreatesKeys)
        )
            return;

        try
        {
            // The pass reads the shared state and dispatches through the platform, so it waits for the replica to
            // materialize its platform, as every hosted service that reads the store does.
            await MaterializedPlatformAccessor.GetPlatformAsync(stoppingToken);
            await RunAsync(stoppingToken);

            using var timer = new PeriodicTimer(RemoteKeys.SweepInterval, TimeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>
    /// Runs one pass, when this replica wins the pass's lease. A pass that fails is logged and run again at the next
    /// interval.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous
    /// operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunCoreAsync(cancellationToken);
        }
        // A timeout throws OperationCanceledException with the token untouched; only a cancelled token is shutdown.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.RemoteKeySweepPassFailed(exception);
        }
    }

    private async ValueTask RunCoreAsync(CancellationToken cancellationToken)
    {
        // The lease lasts half the interval, as the master key maintenance's does: one that lasted the whole interval
        // would still be held when the same replica's timer ticks next.
        var leaseExpiresWhen = TimeProvider.GetUtcNow() + RemoteKeys.SweepInterval / 2;
        if (!await ExpiringKeySet.TryAddAsync(LeaseKey, leaseExpiresWhen, cancellationToken))
            return;

        foreach (
            var descriptor in RemoteKeyDescriptorCatalog
                .Providers.Where(provider => provider.CreatesKeys)
                .OrderBy(x => x.Name, StringComparer.Ordinal)
        )
        {
            // One provider whose dispatch fails must not stop the pass, as the master key maintenance handles each owner.
            try
            {
                await JobDispatcher.DispatchAsync(
                    new RemoteKeySweepJob { Provider = descriptor.Name },
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.RemoteKeySweepDispatchFailed(descriptor.Name, exception);
            }
        }
    }
}
