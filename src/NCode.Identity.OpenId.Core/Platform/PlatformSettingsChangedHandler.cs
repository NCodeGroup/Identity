// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.SharedState;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Handles <see cref="PlatformSettingsChangedCommand"/> by applying the change on this replica before the write returns,
/// then announcing it to every replica. The handler reads the server's committed settings again through the
/// <see cref="IMaterializedPlatformRegistry"/>, so this replica serves the change at once, and then publishes the server
/// identifier on the <see cref="PlatformSettingsChangeBroadcastHandler.ChannelName"/> channel of the
/// <see cref="IReplicaBroadcaster"/> so every other replica reads the settings again too. The refresh and the
/// announcement run on their own bounded token, not the request's, and any failure of either is logged. The
/// announcement follows the local refresh even when the refresh fails, because the change is committed either way.
/// Neither failure fails the committed write: a replica that did not apply the change picks it up at its next poll.
/// </summary>
// See ADR-1219 and ADR-1351.
internal sealed class PlatformSettingsChangedHandler(
    IMaterializedPlatformRegistry materializedServerRegistry,
    IReplicaBroadcaster replicaBroadcaster,
    TimeProvider timeProvider,
    ILogger<PlatformSettingsChangedHandler> logger
) : ICommandHandler<PlatformSettingsChangedCommand>
{
    private IMaterializedPlatformRegistry MaterializedPlatformRegistry { get; } =
        materializedServerRegistry;
    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<PlatformSettingsChangedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        PlatformSettingsChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        // The change is committed, so neither step takes the request's token: each runs on its own bounded token, and a
        // client that disconnects cannot skip them. See ADR-1351.
        await PostCommitWork.RunAsync(TimeProvider, RefreshAsync, command.PlatformId);
        await PostCommitWork.RunAsync(TimeProvider, BroadcastAsync, command.PlatformId);
    }

    /// <summary>
    /// Reads the server's committed settings again on this replica. A failure is logged and swallowed, because the
    /// change is already committed.
    /// </summary>
    private async ValueTask RefreshAsync(string platformId, CancellationToken cancellationToken)
    {
        try
        {
            await MaterializedPlatformRegistry.RefreshSettingsAsync(platformId, cancellationToken);
        }
        catch (Exception exception)
        {
            // The change is committed, so a failed refresh only delays this replica until its next poll. The
            // announcement still goes out. No caller token reaches the refresh, so every failure, its timeout
            // included, is logged.
            Logger.PlatformSettingsChangeRefreshFailed(platformId, exception);
        }
    }

    /// <summary>
    /// Announces the changed server settings to every replica, this one included. A failure is logged and swallowed,
    /// because the change is already committed.
    /// </summary>
    private async ValueTask BroadcastAsync(string platformId, CancellationToken cancellationToken)
    {
        try
        {
            await ReplicaBroadcaster.PublishAsync(
                PlatformSettingsChangeBroadcastHandler.ChannelName,
                platformId,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            // The change is already committed and this replica already applied it, so a failed announcement only
            // delays the other replicas until their polls. No caller token reaches the announcement, so every failure,
            // its timeout included, is logged.
            Logger.PlatformSettingsChangeBroadcastFailed(platformId, exception);
        }
    }
}
