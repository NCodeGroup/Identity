// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Events;

/// <summary>
/// A universal subscriber that emits a metric for every published event, so metrics hang off the same
/// seam as logging and audit. The counter is tagged with bounded dimensions only: the event type, for audit events the
/// event name, event category and outcome, and whatever bounded attributes the event declares through
/// <see cref="ISupportMetricAttributes"/>. High-cardinality fields such as subject or tenant are never dimensions;
/// the tenant's environment type, unlike the tenant id, is a bounded, registered value, so an audit event that carries
/// one is tagged with it.
/// </summary>
// See ADR-1197.
internal class DefaultEventMetricsHandler : IEventHandler<IEvent>, ISupportHandlerPriority
{
    /// <summary>
    /// The name of the counter that records the number of published events.
    /// </summary>
    public const string EventsPublishedCounterName = "ncode.identity.events.published";

    internal const string EventTypeAttribute = "ncode.identity.event.type";

    internal const string AuditEventNameAttribute = "ncode.identity.audit.event_name";

    internal const string AuditEventCategoryAttribute = "ncode.identity.audit.event_category";

    internal const string AuditOutcomeAttribute = "ncode.identity.audit.outcome";

    internal const string TenantEnvironmentTypeAttribute = "ncode.identity.tenant.environment_type";

    private Counter<long> EventsPublished { get; }

    public DefaultEventMetricsHandler(IMeterFactory meterFactory)
    {
        // The factory owns the meter and disposes it with the service provider.
        var meter = meterFactory.Create(EventMetrics.MeterName);
        EventsPublished = meter.CreateCounter<long>(
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
        var tags = new TagList { { EventTypeAttribute, @event.GetType().Name } };

        if (@event is IAuditEvent auditEvent)
        {
            tags.Add(AuditEventNameAttribute, auditEvent.EventName);
            tags.Add(AuditEventCategoryAttribute, auditEvent.EventCategory);
            tags.Add(AuditOutcomeAttribute, auditEvent.Outcome);

            if (auditEvent.TenantEnvironmentType is { } environmentType)
                tags.Add(TenantEnvironmentTypeAttribute, environmentType);
        }

        if (@event is ISupportMetricAttributes supportMetricAttributes)
            supportMetricAttributes.AddMetricAttributes(ref tags);

        EventsPublished.Add(1, tags);
        return ValueTask.CompletedTask;
    }
}
