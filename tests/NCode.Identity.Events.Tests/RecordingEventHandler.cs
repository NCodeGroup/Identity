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

namespace NCode.Identity.Events;

/// <summary>
/// A test event handler that records the events it observes, appends its name to a shared invocation
/// order, and can be configured with a priority and to throw.
/// </summary>
internal sealed class RecordingEventHandler<TEvent>(
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
