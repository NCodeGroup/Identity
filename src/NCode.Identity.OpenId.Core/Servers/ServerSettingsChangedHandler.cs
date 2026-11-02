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
/// <see cref="IReplicaBroadcaster"/> so every other replica reads the settings again too. The refresh and the
/// announcement run on their own bounded token, not the request's, and any failure of either is logged. The
/// announcement follows the local refresh even when the refresh fails, because the change is committed either way.
/// Neither failure fails the committed write: a replica that did not apply the change picks it up at its next poll.
/// </summary>
// See ADR-1219 and ADR-1351.
internal sealed class ServerSettingsChangedHandler(
    IMaterializedServerRegistry materializedServerRegistry,
    IReplicaBroadcaster replicaBroadcaster,
    TimeProvider timeProvider,
    ILogger<ServerSettingsChangedHandler> logger
) : ICommandHandler<ServerSettingsChangedCommand>
{
    /// <summary>
    /// Gets the time the refresh and the announcement that follow a committed change may each take.
    /// </summary>
    internal static TimeSpan PostCommitTimeout => TimeSpan.FromSeconds(30);

    private IMaterializedServerRegistry MaterializedServerRegistry { get; } =
        materializedServerRegistry;
    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<ServerSettingsChangedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ServerSettingsChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        // The change is committed, so neither step takes the request's token: each runs on its own bounded token, and a
        // client that disconnects cannot skip them. See ADR-1351.
        await RefreshAsync(command.ServerId);
        await BroadcastAsync(command.ServerId);
    }

    /// <summary>
    /// Reads the server's committed settings again on this replica. A failure is logged and swallowed, because the
    /// change is already committed.
    /// </summary>
    private async ValueTask RefreshAsync(string serverId)
    {
        using var timeoutSource = new CancellationTokenSource(PostCommitTimeout, TimeProvider);

        try
        {
            await MaterializedServerRegistry.RefreshSettingsAsync(serverId, timeoutSource.Token);
        }
        catch (Exception exception)
        {
            // The change is committed, so a failed refresh only delays this replica until its next poll. The
            // announcement still goes out. No caller token reaches the refresh, so every failure, its timeout
            // included, is logged.
            Logger.ServerSettingsChangeRefreshFailed(serverId, exception);
        }
    }

    /// <summary>
    /// Announces the changed server settings to every replica, this one included. A failure is logged and swallowed,
    /// because the change is already committed.
    /// </summary>
    private async ValueTask BroadcastAsync(string serverId)
    {
        using var timeoutSource = new CancellationTokenSource(PostCommitTimeout, TimeProvider);

        try
        {
            await ReplicaBroadcaster.PublishAsync(
                ServerSettingsChangeBroadcastHandler.ChannelName,
                serverId,
                timeoutSource.Token
            );
        }
        catch (Exception exception)
        {
            // The change is already committed and this replica already applied it, so a failed announcement only
            // delays the other replicas until their polls. No caller token reaches the announcement, so every failure,
            // its timeout included, is logged.
            Logger.ServerSettingsChangeBroadcastFailed(serverId, exception);
        }
    }
}
