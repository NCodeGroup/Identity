// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Handles <see cref="TeamChangedCommand"/> by evicting the team and its tenants from this replica's
/// <see cref="IOpenIdTeamCache"/> and <see cref="IOpenIdTenantCache"/>, then announcing the change on the
/// <see cref="TeamChangeBroadcastHandler.ChannelName"/> channel of the <see cref="IReplicaBroadcaster"/> so every other
/// replica evicts them too. When the team's settings changed it then asks the <see cref="ITeamSettingsReconciliationTrigger"/>
/// to reconcile the team. The eviction and the announcement run on their own bounded token, not the request's, and a
/// failed or timed-out announcement is logged and never fails the committed change: a replica that missed it picks the
/// change up through its polls.
/// </summary>
// See ADR-1411
internal sealed class TeamChangedHandler(
    IOpenIdTeamCache teamCache,
    IOpenIdTenantCache tenantCache,
    IReplicaBroadcaster replicaBroadcaster,
    ITeamSettingsReconciliationTrigger reconciliationTrigger,
    TimeProvider timeProvider,
    ILogger<TeamChangedHandler> logger
) : ICommandHandler<TeamChangedCommand>
{
    private IOpenIdTeamCache TeamCache { get; } = teamCache;
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;
    private ITeamSettingsReconciliationTrigger ReconciliationTrigger { get; } =
        reconciliationTrigger;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<TeamChangedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        TeamChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await PostCommitWork.RunAsync(TimeProvider, EvictAsync, command.TeamId);
        }
        finally
        {
            await PostCommitWork.RunAsync(TimeProvider, BroadcastAsync, command.TeamId);
        }

        // The background service reconciles what the team's settings affect; the trigger never blocks or throws.
        if (command.SettingsChanged)
            ReconciliationTrigger.Trigger(command.TeamId);
    }

    private async ValueTask EvictAsync(string teamId, CancellationToken cancellationToken)
    {
        var propertyBag = PropertyBagFactory.Create();
        await TeamCache.RemoveAsync(teamId, propertyBag, cancellationToken);
        await TenantCache.RemoveTeamAsync(teamId, propertyBag, cancellationToken);
    }

    private async ValueTask BroadcastAsync(string teamId, CancellationToken cancellationToken)
    {
        try
        {
            await ReplicaBroadcaster.PublishAsync(
                TeamChangeBroadcastHandler.ChannelName,
                teamId,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            // The change is already committed and this replica already evicted the team, so a failed announcement only
            // delays the other replicas until their polls.
            Logger.TeamChangeBroadcastFailed(teamId, exception);
        }
    }
}
