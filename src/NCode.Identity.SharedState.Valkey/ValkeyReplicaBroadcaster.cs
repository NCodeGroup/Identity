// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Provides the Valkey implementation of <see cref="IReplicaBroadcaster"/>: a message is one <c>PUBLISH</c> command on
/// the channel named by the <see cref="ValkeyOptions.KeyPrefix"/> followed by the channel, and the server delivers it to
/// every replica subscribed to that channel, this replica's subscription included. The key prefix therefore also
/// separates the channels of deployments that share one server. Delivery is best effort and at most once: a replica that
/// is disconnected when the message is published never receives it. A command against an unreachable server throws, so
/// the publisher can log the loss. The access-control user of the connection needs the <c>PUBLISH</c> and
/// <c>SUBSCRIBE</c> commands.
/// </summary>
// See ADR-1205.
internal sealed class ValkeyReplicaBroadcaster(
    IValkeyConnection connection,
    IOptions<ValkeyOptions> optionsAccessor,
    ValkeySharedStateMetrics metrics
) : IReplicaBroadcaster
{
    private IValkeyConnection Connection { get; } = connection;

    private string KeyPrefix { get; } = optionsAccessor.Value.KeyPrefix;

    private ValkeySharedStateMetrics Metrics { get; } = metrics;

    /// <inheritdoc />
    public async ValueTask PublishAsync(
        string channel,
        string message,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);

        await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.BroadcastPublishOperation,
            async () =>
            {
                var subscriber = await Connection.GetSubscriberAsync(cancellationToken);
                await subscriber
                    .PublishAsync(RedisChannel.Literal(KeyPrefix + channel), message)
                    .WaitAsync(cancellationToken);
            }
        );
    }
}
