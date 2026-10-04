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

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Logging;

namespace NCode.Identity.Events;

/// <summary>
/// Provides the default implementation of <see cref="IBackgroundEventQueue"/> backed by a bounded
/// in-memory channel. Enqueuing never blocks: when the channel is full the event is dropped and the
/// drop is logged (best-effort back-pressure).
/// </summary>
internal class DefaultBackgroundEventQueue(ILogger<DefaultBackgroundEventQueue> logger)
    : IBackgroundEventQueue
{
    private const int Capacity = 1024;

    private ILogger<DefaultBackgroundEventQueue> Logger { get; } = logger;

    private Channel<IEvent> Channel { get; } =
        System.Threading.Channels.Channel.CreateBounded<IEvent>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            }
        );

    // The dispatcher drains this reader; it is an implementation detail, not part of the abstraction.
    public ChannelReader<IEvent> Reader => Channel.Reader;

    /// <inheritdoc />
    public ValueTask EnqueueAsync(IEvent @event, CancellationToken cancellationToken)
    {
        // Non-blocking: TryWrite returns false when the bounded channel is full, so a slow drain can
        // never stall the publishing path.
        if (!Channel.Writer.TryWrite(@event))
        {
            Logger.BackgroundEventDropped(@event.GetType().FullName ?? @event.GetType().Name);
        }

        return ValueTask.CompletedTask;
    }
}
