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
/// subject, client, and tenant), so the full audit envelope is attached for correlation.
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
            name = auditEvent.Action;
            tags["audit.action"] = auditEvent.Action;
            tags["audit.outcome"] = auditEvent.Outcome;
            tags["audit.event_id"] = auditEvent.EventId;
            tags["audit.correlation_id"] = auditEvent.CorrelationId;
            tags["audit.tenant_id"] = auditEvent.TenantId;
            tags["audit.subject_id"] = auditEvent.SubjectId;
            tags["audit.client_id"] = auditEvent.ClientId;
        }

        activity.AddEvent(new ActivityEvent(name, tags: tags));
        return ValueTask.CompletedTask;
    }
}
