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

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Logging;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Reconciles everything derived from the server's effective settings in the background, whenever those settings
/// differ from the ones they were last reconciled against. The service waits until this replica materializes its
/// server, which is its first request, so it never reads the database while the host starts. It then reconciles once,
/// and again whenever the server's settings provider signals a change: a configuration reload, a read of the committed
/// settings document, or a poll that saw a change. Signals that arrive during a reconciliation coalesce into one more.
/// A reconciliation that is unsettled, because another replica holds its lease or the pass failed, is attempted again
/// when the <see cref="OpenIdServerOptions.ReconciliationLeaseDuration"/> has passed. The service never throws on
/// stop.
/// </summary>
// See ADR-0219.
internal sealed class ServerSettingsReconciliationService(
    IMaterializedServerRegistry materializedServerRegistry,
    IServerSettingsReconciler serverSettingsReconciler,
    IOptions<OpenIdOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<ServerSettingsReconciliationService> logger
) : BackgroundService
{
    private IMaterializedServerRegistry MaterializedServerRegistry { get; } =
        materializedServerRegistry;
    private IServerSettingsReconciler ServerSettingsReconciler { get; } = serverSettingsReconciler;
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<ServerSettingsReconciliationService> Logger { get; } = logger;

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
            // Never at host start: the server is materialized by the replica's first request.
            var server = await MaterializedServerRegistry.GetServerAsync(stoppingToken);

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
                settled = await ReconcileAsync(server.ServerId, stoppingToken);
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown; a hosted service never throws on stop.
        }
    }

    private void Trigger() => Triggers.Writer.TryWrite(true);

    private async Task WaitForTriggerAsync(bool settled, CancellationToken stoppingToken)
    {
        if (settled)
        {
            await Triggers.Reader.ReadAsync(stoppingToken);
            return;
        }

        // An unsettled reconciliation is attempted again when the lease another replica may hold has expired.
        using var leaseExpired = new CancellationTokenSource(
            Options.Server.ReconciliationLeaseDuration,
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

    private async Task<bool> ReconcileAsync(string serverId, CancellationToken stoppingToken)
    {
        try
        {
            return await ServerSettingsReconciler.ReconcileAsync(serverId, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.ServerSettingsReconciliationFailed(serverId, exception);
            return false;
        }
    }
}
