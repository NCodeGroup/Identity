// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Logging;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Reconciles everything derived from the server's effective settings in the background, whenever those settings
/// differ from the ones they were last reconciled against. The service materializes the server when the replica starts
/// and reconciles it; the recorded fingerprint makes a start that changes nothing cost one read. When materializing at
/// start fails, it waits until a request materializes the server. It reconciles once, and again whenever the server's
/// settings provider signals a change: a configuration reload, a read of the committed settings document, or a poll
/// that saw a change. Signals that arrive during a reconciliation coalesce into one more. A reconciliation that is
/// unsettled, because another replica holds its lease or the pass failed, is attempted again when the
/// <see cref="OpenIdPlatformOptions.ReconciliationLeaseDuration"/> has passed. The service never throws on stop.
/// </summary>
// See ADR-1219.
internal sealed class PlatformSettingsReconciliationService(
    IMaterializedPlatformRegistry materializedServerRegistry,
    IOpenIdPlatformProvider serverProvider,
    IOpenIdEnvironmentProvider environmentProvider,
    IPlatformSettingsReconciler serverSettingsReconciler,
    IOptions<OpenIdOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<PlatformSettingsReconciliationService> logger
) : BackgroundService
{
    private IMaterializedPlatformRegistry MaterializedPlatformRegistry { get; } =
        materializedServerRegistry;
    private IOpenIdPlatformProvider PlatformProvider { get; } = serverProvider;
    private IOpenIdEnvironmentProvider EnvironmentProvider { get; } = environmentProvider;
    private IPlatformSettingsReconciler PlatformSettingsReconciler { get; } =
        serverSettingsReconciler;
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<PlatformSettingsReconciliationService> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private OpenIdOptions Options => OptionsAccessor.Value;

    // Triggers that arrive while a reconciliation runs coalesce into one more.
    private Channel<bool> Triggers { get; } =
        Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite }
        );

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var server = await GetPlatformAsync(stoppingToken);

            // The provider signals a configuration reload, a refresh of the committed document, and a poll that saw one.
            using var registration = ChangeToken.OnChange(
                server.SettingsProvider.GetChangeToken,
                Trigger
            );
            Trigger();

            var settled = true;
            while (!stoppingToken.IsCancellationRequested)
            {
                await WaitForTriggerAsync(settled, stoppingToken);
                settled = await ReconcileAsync(server.PlatformId, stoppingToken);
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown; a hosted service never throws on stop.
        }
    }

    private void Trigger() => Triggers.Writer.TryWrite(true);

    private async ValueTask<OpenIdPlatform> GetPlatformAsync(CancellationToken stoppingToken)
    {
        try
        {
            var environment = EnvironmentProvider.Get();
            return await PlatformProvider.GetAsync(environment, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // The database may be unreachable while the replica starts; the first request materializes the server.
            Logger.PlatformMaterializationAtStartFailed(exception);
            return await MaterializedPlatformRegistry.GetPlatformAsync(stoppingToken);
        }
    }

    private async Task WaitForTriggerAsync(bool settled, CancellationToken stoppingToken)
    {
        if (settled)
        {
            await Triggers.Reader.ReadAsync(stoppingToken);
            return;
        }

        // An unsettled reconciliation is attempted again when the lease another replica may hold has expired.
        using var leaseExpired = new CancellationTokenSource(
            Options.Platform.ReconciliationLeaseDuration,
            TimeProvider
        );
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            leaseExpired.Token
        );
        try
        {
            await Triggers.Reader.ReadAsync(wait.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // The lease expired.
        }
    }

    private async Task<bool> ReconcileAsync(string platformId, CancellationToken stoppingToken)
    {
        try
        {
            return await PlatformSettingsReconciler.ReconcileAsync(platformId, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.PlatformSettingsReconciliationFailed(platformId, exception);
            return false;
        }
    }
}
