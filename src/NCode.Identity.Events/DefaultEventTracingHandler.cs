// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Events;

/// <summary>
/// A universal subscriber that records each published event as an <see cref="ActivityEvent"/> on the
/// current <see cref="Activity"/>, so published events appear on distributed traces alongside the same
/// seam that drives logging, audit, and metrics. It is a no-op when there is no current activity.
/// </summary>
/// <remarks>
/// Unlike metric dimensions, span-event attributes may carry higher-cardinality fields (such as
/// subject, client, and tenant), so the flattened audit record (<c>audit.*</c> keys, the same ones the
/// audit log entry carries) is attached for correlation.
/// </remarks>
internal class DefaultEventTracingHandler : IEventHandler<IEvent>, ISupportHandlerPriority
{
    /// <inheritdoc />
    public int HandlerPriority => DefaultHandlerPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(IEvent @event, CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        if (activity is null)
            return ValueTask.CompletedTask;

        var eventType = @event.GetType().Name;
        var tags = new ActivityTagsCollection { { "event.type", eventType } };

        var name = eventType;
        if (@event is IAuditEvent auditEvent)
        {
            name = auditEvent.EventName;
            foreach (var (key, value) in AuditRecordAttributes.Create(auditEvent))
            {
                tags[key] = value;
            }
        }

        activity.AddEvent(new ActivityEvent(name, tags: tags));
        return ValueTask.CompletedTask;
    }
}
