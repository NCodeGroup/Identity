// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Reloads the server's secret protector at once when another replica announces that the server's master key changed,
/// and after missed announcements, instead of waiting for the next refresh. The general handler of the channel evicts
/// the owner's keys from the ring; this one also publishes the new protector to the holder that encrypts the Data
/// Protection key ring.
/// </summary>
// See ADR-1407.
internal sealed class ServerMasterKeyChangeBroadcastHandler(
    ServerSecretProtectorLoader loader,
    ISecretProtectorProvider provider
) : IReplicaBroadcastHandler
{
    private ServerSecretProtectorLoader Loader { get; } = loader;

    private ISecretProtectorProvider Provider { get; } = provider;

    /// <inheritdoc />
    public string Channel => MasterKeyBroadcast.ChannelName;

    /// <inheritdoc />
    public async ValueTask HandleAsync(string message, CancellationToken cancellationToken)
    {
        if (!SecretOwner.TryParse(message, out var owner))
            return;

        var server = Loader.Owner;
        if (
            !string.Equals(owner.OwnerType, server.OwnerType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(owner.OwnerId, server.OwnerId, StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        Provider.Evict(server);
        await Loader.LoadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken)
    {
        Provider.EvictAll();
        await Loader.LoadAsync(cancellationToken);
    }
}
