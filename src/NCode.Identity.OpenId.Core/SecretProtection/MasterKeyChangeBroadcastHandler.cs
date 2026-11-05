// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Evicts the master keys of the owner that another replica announced, so that the next use reads them again. A
/// missed announcement evicts every owner; the refresh interval bounds a lost one.
/// </summary>
// See ADR-1407.
internal sealed class MasterKeyChangeBroadcastHandler(ISecretProtectorProvider protectorProvider)
    : IReplicaBroadcastHandler
{
    private ISecretProtectorProvider ProtectorProvider { get; } = protectorProvider;

    /// <inheritdoc />
    public string Channel => MasterKeyBroadcast.ChannelName;

    /// <inheritdoc />
    public ValueTask HandleAsync(string message, CancellationToken cancellationToken)
    {
        // A message that names no owner is ignored: the owner reads its keys again when the refresh interval passes.
        if (SecretOwner.TryParse(message, out var owner))
            ProtectorProvider.Evict(owner);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken)
    {
        ProtectorProvider.EvictAll();

        return ValueTask.CompletedTask;
    }
}
