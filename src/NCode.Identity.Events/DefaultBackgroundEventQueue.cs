// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
