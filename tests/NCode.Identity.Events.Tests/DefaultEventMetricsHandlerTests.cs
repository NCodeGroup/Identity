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
    public async Task HandleAsync_WhenAuditEvent_RecordsCounterWithActionAndOutcomeTags()
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
            measurement.Tags[DefaultEventMetricsHandler.AuditActionAttribute]
        );
        Assert.Equal(
            AuditOutcome.Failure,
            measurement.Tags[DefaultEventMetricsHandler.AuditOutcomeAttribute]
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
