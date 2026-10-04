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

using System.Diagnostics.Metrics;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Events;

public sealed class DefaultEventMetricsHandlerTests
{
    private sealed record SampleEvent : IEvent;

    private static (
        DefaultEventMetricsHandler Handler,
        List<(long Value, Dictionary<string, object?> Tags)> Measurements,
        MeterListener Listener
    ) CreateListening()
    {
        var handler = new DefaultEventMetricsHandler();
        var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == DefaultEventMetricsHandler.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (_, value, tags, _) =>
            {
                var captured = new Dictionary<string, object?>();
                foreach (var tag in tags)
                    captured[tag.Key] = tag.Value;
                measurements.Add((value, captured));
            }
        );
        listener.Start();

        return (handler, measurements, listener);
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAuditEvent_RecordsCounterWithActionAndOutcomeTags()
    {
        var (handler, measurements, listener) = CreateListening();
        using var _ = handler;
        using var __ = listener;

        await handler.HandleAsync(
            new TestAuditEvent { Outcome = AuditOutcome.Failure },
            CancellationToken.None
        );

        var measurement = Assert.Single(measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal(nameof(TestAuditEvent), measurement.Tags["event.type"]);
        Assert.Equal("test.action", measurement.Tags["audit.action"]);
        Assert.Equal(AuditOutcome.Failure, measurement.Tags["audit.outcome"]);
    }

    [Fact]
    public async Task HandleAsync_WhenPlainEvent_RecordsCounterWithEventTypeOnly()
    {
        var (handler, measurements, listener) = CreateListening();
        using var _ = handler;
        using var __ = listener;

        await handler.HandleAsync(new SampleEvent(), CancellationToken.None);

        var measurement = Assert.Single(measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal(nameof(SampleEvent), measurement.Tags["event.type"]);
        Assert.DoesNotContain("audit.action", measurement.Tags.Keys);
    }

    #endregion
}
