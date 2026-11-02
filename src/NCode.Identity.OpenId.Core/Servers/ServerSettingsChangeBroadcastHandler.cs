// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Handles the announcement that a server's settings changed by reading the committed settings of the announced server
/// again on this replica. Every replica receives the announcement, the one that made the change included, so each
/// serves the change at once rather than at its next poll. The message is the server identifier, never the settings
/// document: the replica reads the committed row. Reading again is idempotent, so the second read on the replica that
/// made the change, and a repeated message, are harmless. A replica that may have missed an announcement reads the
/// settings again too.
/// </summary>
// See ADR-1205 and ADR-1219.
internal sealed class ServerSettingsChangeBroadcastHandler(
    IMaterializedServerRegistry materializedServerRegistry
) : IReplicaBroadcastHandler
{
    /// <summary>
    /// The name of the replica broadcast channel on which a changed server's settings are announced.
    /// </summary>
    public const string ChannelName = "server-settings-changed";

    private IMaterializedServerRegistry MaterializedServerRegistry { get; } =
        materializedServerRegistry;

    /// <inheritdoc />
    public string Channel => ChannelName;

    /// <inheritdoc />
    public ValueTask HandleAsync(string message, CancellationToken cancellationToken) =>
        MaterializedServerRegistry.RefreshSettingsAsync(message, cancellationToken);

    /// <inheritdoc />
    public ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken) =>
        MaterializedServerRegistry.RefreshSettingsAsync(cancellationToken);
}
