// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Reloads the platform's secret protector at once when another replica announces that the platform's master key changed,
/// and after missed announcements, instead of waiting for the next refresh. The general handler of the channel evicts
/// the owner's keys from the ring; this one also publishes the new protector to the holder that encrypts the Data
/// Protection key ring.
/// </summary>
// See ADR-1407.
internal sealed class PlatformMasterKeyChangeBroadcastHandler(
    PlatformSecretProtectorLoader loader,
    ISecretProtectorProvider provider
) : IReplicaBroadcastHandler
{
    private PlatformSecretProtectorLoader Loader { get; } = loader;

    private ISecretProtectorProvider Provider { get; } = provider;

    /// <inheritdoc />
    public string Channel => MasterKeyBroadcast.ChannelName;

    /// <inheritdoc />
    public async ValueTask HandleAsync(string message, CancellationToken cancellationToken)
    {
        if (!SecretOwner.TryParse(message, out var owner))
            return;

        if (!Loader.IsOwner(owner))
            return;

        Provider.Evict(Loader.Owner);
        await Loader.LoadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken)
    {
        Provider.EvictAll();
        await Loader.LoadAsync(cancellationToken);
    }
}
