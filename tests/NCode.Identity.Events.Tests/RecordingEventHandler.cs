// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Events;

/// <summary>
/// A test event handler that records the events it observes, appends its name to a shared invocation
/// order, and can be configured with a priority and to throw.
/// </summary>
internal class RecordingEventHandler<TEvent>(
    string name,
    IList<string> invocationOrder,
    int priority = 0,
    bool throws = false
) : IEventHandler<TEvent>, ISupportHandlerPriority
    where TEvent : IEvent
{
    public List<TEvent> Received { get; } = [];

    public int HandlerPriority => priority;

    public ValueTask HandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        invocationOrder.Add(name);
        Received.Add(@event);

        if (throws)
            throw new InvalidOperationException($"Handler '{name}' failed.");

        return ValueTask.CompletedTask;
    }
}
