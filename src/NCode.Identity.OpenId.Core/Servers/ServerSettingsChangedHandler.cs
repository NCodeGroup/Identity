// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.SharedState;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Handles <see cref="ServerSettingsChangedCommand"/> by applying the change on this replica before the write returns,
/// then announcing it to every replica. The handler reads the server's committed settings again through the
/// <see cref="IMaterializedServerRegistry"/>, so this replica serves the change at once, and then publishes the server
/// identifier on the <see cref="ServerSettingsChangeBroadcastHandler.ChannelName"/> channel of the
/// <see cref="IReplicaBroadcaster"/> so every other replica reads the settings again too. The announcement follows the
/// local refresh even when the refresh fails, because the change is committed either way. Neither failure fails the
/// committed write: a replica that did not apply the change picks it up at its next poll.
/// </summary>
// See ADR-1219.
internal sealed class ServerSettingsChangedHandler(
    IMaterializedServerRegistry materializedServerRegistry,
    IReplicaBroadcaster replicaBroadcaster,
    ILogger<ServerSettingsChangedHandler> logger
) : ICommandHandler<ServerSettingsChangedCommand>
{
    private IMaterializedServerRegistry MaterializedServerRegistry { get; } =
        materializedServerRegistry;
    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;
    private ILogger<ServerSettingsChangedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ServerSettingsChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await MaterializedServerRegistry.RefreshSettingsAsync(
                command.ServerId,
                cancellationToken
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change is committed, so a failed refresh only delays this replica until its next poll. The
            // announcement still goes out.
            Logger.ServerSettingsChangeRefreshFailed(command.ServerId, exception);
        }

        await BroadcastAsync(command.ServerId, cancellationToken);
    }

    /// <summary>
    /// Announces the changed server settings to every replica, this one included. A failure is logged and swallowed,
    /// because the change is already committed.
    /// </summary>
    private async ValueTask BroadcastAsync(string serverId, CancellationToken cancellationToken)
    {
        try
        {
            await ReplicaBroadcaster.PublishAsync(
                ServerSettingsChangeBroadcastHandler.ChannelName,
                serverId,
                cancellationToken
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change is already committed and this replica already applied it, so a failed announcement only
            // delays the other replicas until their polls.
            Logger.ServerSettingsChangeBroadcastFailed(serverId, exception);
        }
    }
}
