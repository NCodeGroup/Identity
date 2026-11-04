// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.SharedState;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Handles the announcement that a team changed by evicting the team from this replica's <see cref="IOpenIdTeamCache"/>
/// and its tenants from the <see cref="IOpenIdTenantCache"/>. Every replica receives the announcement, the one that made
/// the change included. The message is the team identifier. Evicting is idempotent, so a repeated message is harmless. A
/// replica that may have missed an announcement evicts every cached team and every cached tenant.
/// </summary>
// See ADR-1403
internal sealed class TeamChangeBroadcastHandler(
    IOpenIdTeamCache teamCache,
    IOpenIdTenantCache tenantCache,
    ILogger<TeamChangeBroadcastHandler> logger
) : IReplicaBroadcastHandler
{
    /// <summary>
    /// The name of the replica broadcast channel on which a changed team is announced.
    /// </summary>
    public const string ChannelName = "team-changed";

    private IOpenIdTeamCache TeamCache { get; } = teamCache;
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private ILogger<TeamChangeBroadcastHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public string Channel => ChannelName;

    /// <inheritdoc />
    public async ValueTask HandleAsync(string message, CancellationToken cancellationToken)
    {
        var propertyBag = PropertyBagFactory.Create();
        await TeamCache.RemoveAsync(message, propertyBag, cancellationToken);
        await TenantCache.RemoveTeamAsync(message, propertyBag, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken)
    {
        var propertyBag = PropertyBagFactory.Create();

        // Each cache is cleared on its own, so a failed team-cache clear still clears the tenants.
        try
        {
            await TeamCache.ClearAsync(propertyBag, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TeamCacheClearFailed(exception);
        }

        try
        {
            await TenantCache.ClearAsync(propertyBag, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TenantCacheClearFailed(exception);
        }
    }
}
