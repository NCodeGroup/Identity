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
using System.Diagnostics.Metrics;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Events;

/// <summary>
/// A universal subscriber that emits a metric for every published event, so metrics hang off the same
/// seam as logging and audit. The counter is tagged with low-cardinality dimensions only (the event
/// type, and for audit events the action and outcome); high-cardinality fields such as subject or
/// tenant are never used as metric dimensions.
/// </summary>
internal class DefaultEventMetricsHandler
    : IEventHandler<IEvent>,
        ISupportHandlerPriority,
        IDisposable
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> that emits event metrics.
    /// </summary>
    public const string MeterName = "NCode.Identity.Events";

    /// <summary>
    /// The name of the counter that records the number of published events.
    /// </summary>
    public const string EventsPublishedCounterName = "ncode.identity.events.published";

    private Meter Meter { get; }
    private Counter<long> EventsPublished { get; }

    public DefaultEventMetricsHandler()
    {
        Meter = new Meter(MeterName);
        EventsPublished = Meter.CreateCounter<long>(
            EventsPublishedCounterName,
            unit: "{event}",
            description: "The number of events published through the event seam."
        );
    }

    /// <inheritdoc />
    public int HandlerPriority => DefaultHandlerPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(IEvent @event, CancellationToken cancellationToken)
    {
        var tags = new TagList { { "event.type", @event.GetType().Name } };

        if (@event is IAuditEvent auditEvent)
        {
            tags.Add("audit.action", auditEvent.Action);
            tags.Add("audit.outcome", auditEvent.Outcome);
        }

        EventsPublished.Add(1, tags);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => Meter.Dispose();
}
