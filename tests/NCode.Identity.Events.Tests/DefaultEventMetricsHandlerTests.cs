// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Events;

public sealed class DefaultEventMetricsHandlerTests : IDisposable
{
    private sealed record SampleEvent : IEvent;

    private sealed record AttributedEvent : IEvent, ISupportMetricAttributes
    {
        public void AddMetricAttributes(ref TagList tags) =>
            tags.Add("ncode.identity.test.kind", "sample");
    }

    private ServiceProvider ServiceProvider { get; }

    private DefaultEventMetricsHandler Handler { get; }

    private MetricCollector<long> Collector { get; }

    public DefaultEventMetricsHandlerTests()
    {
        ServiceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = ServiceProvider.GetRequiredService<IMeterFactory>();
        Handler = new DefaultEventMetricsHandler(meterFactory);
        Collector = new MetricCollector<long>(
            meterFactory,
            EventMetrics.MeterName,
            DefaultEventMetricsHandler.EventsPublishedCounterName
        );
    }

    public void Dispose()
    {
        Collector.Dispose();
        ServiceProvider.Dispose();
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAuditEvent_RecordsCounterWithEventNameCategoryAndOutcomeTags()
    {
        await Handler.HandleAsync(
            new TestAuditEvent { Outcome = AuditOutcome.Failure },
            CancellationToken.None
        );

        var measurement = Assert.Single(Collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal("{event}", Collector.Instrument?.Unit);
        Assert.Equal(
            nameof(TestAuditEvent),
            measurement.Tags[DefaultEventMetricsHandler.EventTypeAttribute]
        );
        Assert.Equal(
            "test.action",
            measurement.Tags[DefaultEventMetricsHandler.AuditEventNameAttribute]
        );
        Assert.Equal(
            "test",
            measurement.Tags[DefaultEventMetricsHandler.AuditEventCategoryAttribute]
        );
        Assert.Equal(
            AuditOutcome.Failure,
            measurement.Tags[DefaultEventMetricsHandler.AuditOutcomeAttribute]
        );
    }

    [Fact]
    public async Task HandleAsync_WhenAuditEventHasTenantEnvironmentType_RecordsTheTag()
    {
        await Handler.HandleAsync(
            new TestAuditEvent { TenantEnvironmentType = "production" },
            CancellationToken.None
        );

        var measurement = Assert.Single(Collector.GetMeasurementSnapshot());
        Assert.Equal(
            "production",
            measurement.Tags[DefaultEventMetricsHandler.TenantEnvironmentTypeAttribute]
        );
    }

    [Fact]
    public async Task HandleAsync_WhenAuditEventHasNoTenantEnvironmentType_RecordsNoSuchTag()
    {
        await Handler.HandleAsync(new TestAuditEvent(), CancellationToken.None);

        var measurement = Assert.Single(Collector.GetMeasurementSnapshot());
        Assert.False(
            measurement.Tags.ContainsKey(DefaultEventMetricsHandler.TenantEnvironmentTypeAttribute)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenPlainEvent_RecordsCounterWithEventTypeOnly()
    {
        await Handler.HandleAsync(new SampleEvent(), CancellationToken.None);

        var measurement = Assert.Single(Collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        var tag = Assert.Single(measurement.Tags);
        Assert.Equal(DefaultEventMetricsHandler.EventTypeAttribute, tag.Key);
        Assert.Equal(nameof(SampleEvent), tag.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenTheEventDeclaresMetricAttributes_AddsThem()
    {
        await Handler.HandleAsync(new AttributedEvent(), CancellationToken.None);

        var measurement = Assert.Single(Collector.GetMeasurementSnapshot());
        Assert.Equal(
            nameof(AttributedEvent),
            measurement.Tags[DefaultEventMetricsHandler.EventTypeAttribute]
        );
        Assert.Equal("sample", measurement.Tags["ncode.identity.test.kind"]);
    }

    #endregion
}
