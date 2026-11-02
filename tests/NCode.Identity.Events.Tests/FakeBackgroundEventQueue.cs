// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Events;

/// <summary>
/// A test <see cref="IBackgroundEventQueue"/> that records the events handed to it.
/// </summary>
internal sealed class FakeBackgroundEventQueue : IBackgroundEventQueue
{
    public List<IEvent> Enqueued { get; } = [];

    public ValueTask EnqueueAsync(IEvent @event, CancellationToken cancellationToken)
    {
        Enqueued.Add(@event);
        return ValueTask.CompletedTask;
    }
}
