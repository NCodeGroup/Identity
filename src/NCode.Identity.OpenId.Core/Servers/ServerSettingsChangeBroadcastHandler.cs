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
// See ADR-0205 and ADR-0219.
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
