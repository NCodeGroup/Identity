// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Client.Metrics;

namespace NCode.Identity.OpenId.Client.Tests;

/// <summary>
/// Creates the client's <see cref="ProviderMetrics"/> on a meter factory of their own and records every measurement of
/// that factory's meters, so a test sees only the measurements of the metrics it created.
/// </summary>
/// <remarks>
/// It listens with <see cref="MeterListener"/> rather than <c>Microsoft.Extensions.Diagnostics.Testing</c>, whose
/// telemetry dependency the client package must never carry, and which a test project would copy beside it.
/// </remarks>
internal sealed class TestMetrics : IDisposable
{
    private ServiceProvider ServiceProvider { get; }

    private MeterListener Listener { get; }

    private ConcurrentQueue<(
        string Instrument,
        Dictionary<string, object?> Tags
    )> Measurements { get; } = new();

    public TestMetrics()
    {
        ServiceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = ServiceProvider.GetRequiredService<IMeterFactory>();

        Listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter.Scope, meterFactory))
                    listener.EnableMeasurementEvents(instrument);
            },
        };
        Listener.SetMeasurementEventCallback<long>(
            (instrument, _, tags, _) =>
            {
                var bag = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var tag in tags)
                    bag[tag.Key] = tag.Value;
                Measurements.Enqueue((instrument.Name, bag));
            }
        );
        Listener.Start();
    }

    public IMeterFactory MeterFactory => ServiceProvider.GetRequiredService<IMeterFactory>();

    public ProviderMetrics CreateProviderMetrics() => new(MeterFactory);

    /// <summary>
    /// Gets the value of <paramref name="attributeName"/> on every measurement recorded on the instrument named
    /// <paramref name="instrumentName"/> of the <see cref="OpenIdClientMetrics.MeterName"/> meter, in recording order.
    /// </summary>
    public List<string?> Measured(string instrumentName, string attributeName) =>
        Measurements
            .Where(measurement => measurement.Instrument == instrumentName)
            .Select(measurement => measurement.Tags.GetValueOrDefault(attributeName) as string)
            .ToList();

    public void Dispose()
    {
        Listener.Dispose();
        ServiceProvider.Dispose();
    }
}
