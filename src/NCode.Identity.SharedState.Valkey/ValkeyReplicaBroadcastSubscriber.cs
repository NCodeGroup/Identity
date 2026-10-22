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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Logging;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Subscribes this replica to the Valkey channel of every registered <see cref="IReplicaBroadcastHandler"/> at
/// startup and hands each message published on a channel to the handlers of that channel. The subscription is retried
/// every <see cref="RetryDelay"/> while the server is unreachable, and the connection restores it after a reconnect.
/// A message is delivered on a thread-pool thread so a slow handler never blocks the connection's receive loop, and a
/// handler failure is logged and never reaches the publisher. The access-control user of the connection needs the
/// <c>SUBSCRIBE</c> command.
/// </summary>
// See ADR-0205.
internal sealed class ValkeyReplicaBroadcastSubscriber(
    IValkeyConnection connection,
    IOptions<ValkeyOptions> optionsAccessor,
    IEnumerable<IReplicaBroadcastHandler> handlers,
    ILogger<ValkeyReplicaBroadcastSubscriber> logger
) : BackgroundService
{
    private IValkeyConnection Connection { get; } = connection;

    private string KeyPrefix { get; } = optionsAccessor.Value.KeyPrefix;

    private IReadOnlyList<IReplicaBroadcastHandler> Handlers { get; } = [.. handlers];

    private ILogger<ValkeyReplicaBroadcastSubscriber> Logger { get; } = logger;

    /// <summary>
    /// Gets the delay before the subscription is retried after the server could not be reached.
    /// </summary>
    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pending = new Queue<string>(
            Handlers.Select(x => x.Channel).Distinct(StringComparer.Ordinal)
        );

        while (pending.Count > 0)
        {
            try
            {
                var subscriber = await Connection.GetSubscriberAsync(stoppingToken);
                while (pending.TryPeek(out var channel))
                {
                    var redisChannel = RedisChannel.Literal(KeyPrefix + channel);
                    Action<RedisChannel, RedisValue> handler = (_, value) =>
                        OnMessage(channel, value);

                    try
                    {
                        await subscriber
                            .SubscribeAsync(redisChannel, handler)
                            .WaitAsync(stoppingToken);
                    }
                    catch
                    {
                        // The library registers the handler before the server confirms the subscription, so a
                        // failed attempt must take it back, or the retry delivers every message twice.
                        subscriber.Unsubscribe(redisChannel, handler, CommandFlags.FireAndForget);
                        throw;
                    }

                    pending.Dequeue();
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                Logger.ReplicaBroadcastSubscribeFailed(exception);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private void OnMessage(string channel, RedisValue value)
    {
        _ = Task.Run(() => DispatchAsync(channel, value.ToString()));
    }

    /// <summary>
    /// Delivers a message to every handler of the channel. A handler that throws is logged, and the remaining handlers
    /// still receive the message.
    /// </summary>
    /// <param name="channel">The channel the message was published on, without the key prefix.</param>
    /// <param name="message">The message that was published.</param>
    /// <returns>A <see cref="Task"/> that completes once every handler of the channel has run.</returns>
    internal async Task DispatchAsync(string channel, string message)
    {
        foreach (var handler in Handlers)
        {
            if (!string.Equals(handler.Channel, channel, StringComparison.Ordinal))
                continue;

            try
            {
                await handler.HandleAsync(message, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Logger.ReplicaBroadcastHandlerFailed(channel, exception);
            }
        }
    }
}
