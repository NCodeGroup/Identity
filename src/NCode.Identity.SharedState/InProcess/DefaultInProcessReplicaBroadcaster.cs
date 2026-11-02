// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.SharedState.Logging;

namespace NCode.Identity.SharedState.InProcess;

/// <summary>
/// Provides the in-process implementation of <see cref="IReplicaBroadcaster"/>: it delivers a message synchronously to
/// the <see cref="IReplicaBroadcastHandler"/> instances of this process that are registered for the channel. It is
/// correct only for a deployment that runs a single replica, because a message published by one replica is not seen by
/// another. A handler's failure is logged and never reaches the publisher.
/// </summary>
// See ADR-1205 and ADR-1351.
internal sealed class DefaultInProcessReplicaBroadcaster(
    IEnumerable<IReplicaBroadcastHandler> handlers,
    ILogger<DefaultInProcessReplicaBroadcaster> logger
) : IReplicaBroadcaster
{
    private IReadOnlyList<IReplicaBroadcastHandler> Handlers { get; } = [.. handlers];

    private ILogger<DefaultInProcessReplicaBroadcaster> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask PublishAsync(
        string channel,
        string message,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);

        foreach (var handler in Handlers)
        {
            if (!string.Equals(handler.Channel, channel, StringComparison.Ordinal))
                continue;

            try
            {
                await handler.HandleAsync(message, cancellationToken);
            }
            // A handler's own timeout throws OperationCanceledException with the publisher's token untouched; only a
            // cancelled token stops the delivery.
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.ReplicaBroadcastHandlerFailed(channel, exception);
            }
        }
    }
}
