// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Handles the announcement that a platform's settings changed by reading the committed settings of the platform
/// again on this replica. Every replica receives the announcement, the one that made the change included, so each
/// serves the change at once rather than at its next poll. The message is the constant platform resource identifier,
/// never the settings document: the replica reads the committed row. Reading again is idempotent, so the second read on
/// the replica that made the change, and a repeated message, are harmless. A replica that may have missed an
/// announcement reads the settings again too.
/// </summary>
// See ADR-1205 and ADR-1412.
internal sealed class PlatformSettingsChangeBroadcastHandler(
    IMaterializedPlatformRegistry materializedPlatformRegistry
) : IReplicaBroadcastHandler
{
    /// <summary>
    /// The name of the replica broadcast channel on which a changed platform's settings are announced.
    /// </summary>
    public const string ChannelName = "platform-settings-changed";

    private IMaterializedPlatformRegistry MaterializedPlatformRegistry { get; } =
        materializedPlatformRegistry;

    /// <inheritdoc />
    public string Channel => ChannelName;

    /// <inheritdoc />
    public ValueTask HandleAsync(string message, CancellationToken cancellationToken) =>
        MaterializedPlatformRegistry.RefreshSettingsAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken) =>
        MaterializedPlatformRegistry.RefreshSettingsAsync(cancellationToken);
}
