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

using Microsoft.Extensions.Logging;
using NCode.Identity.SharedState.Logging;

namespace NCode.Identity.SharedState.InProcess;

/// <summary>
/// Provides the in-process implementation of <see cref="IReplicaBroadcaster"/>: it delivers a message synchronously to
/// the <see cref="IReplicaBroadcastHandler"/> instances of this process that are registered for the channel. It is
/// correct only for a deployment that runs a single replica, because a message published by one replica is not seen by
/// another. A handler's failure is logged and never reaches the publisher.
/// </summary>
// See ADR-0205.
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
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Logger.ReplicaBroadcastHandlerFailed(channel, exception);
            }
        }
    }
}
