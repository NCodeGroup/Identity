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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace NCode.Identity.Server.Tests.Telemetry;

public sealed class TelemetryRegistrationTests
{
    private static IConfiguration CreateConfiguration(string? otlpEndpoint) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [TelemetryRegistration.OtlpEndpointConfigurationKey] = otlpEndpoint,
                }
            )
            .Build();

    private static bool HasOtlpRegistration(IServiceCollection serviceCollection) =>
        serviceCollection.Any(descriptor =>
            descriptor.ServiceType.ToString().Contains("Otlp", StringComparison.Ordinal)
            || (
                descriptor.ImplementationType?.ToString().Contains("Otlp", StringComparison.Ordinal)
                ?? false
            )
        );

    private sealed class CollectingExporter : BaseExporter<Metric>
    {
        public List<(string Name, Dictionary<string, object?> Tags)> Points { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    var tags = new Dictionary<string, object?>();
                    foreach (var tag in point.Tags)
                        tags[tag.Key] = tag.Value;

                    Points.Add((metric.Name, tags));
                }
            }

            return ExportResult.Success;
        }
    }

    #region AddOpenTelemetryMetrics Tests

    [Fact]
    public void AddOpenTelemetryMetrics_WhenConfigurationIsNull_Throws()
    {
        var serviceCollection = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            serviceCollection.AddOpenTelemetryMetrics(null!)
        );
    }

    [Fact]
    public void AddOpenTelemetryMetrics_WhenNoEndpointIsConfigured_AddsNoExporter()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddOpenTelemetryMetrics(CreateConfiguration(otlpEndpoint: null));

        Assert.False(HasOtlpRegistration(serviceCollection));
    }

    [Fact]
    public void AddOpenTelemetryMetrics_WhenAnEndpointIsConfigured_AddsTheOtlpExporter()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddOpenTelemetryMetrics(CreateConfiguration("http://localhost:4317"));

        Assert.True(HasOtlpRegistration(serviceCollection));
    }

    [Fact]
    public void AddOpenTelemetryMetrics_WhenAnEndpointIsConfigured_ExportsNoLogsOrTraces()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOpenTelemetryMetrics(CreateConfiguration("http://localhost:4317"));

        using var serviceProvider = serviceCollection.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<MeterProvider>());
        Assert.Null(serviceProvider.GetService<TracerProvider>());
        Assert.Empty(serviceProvider.GetServices<ILoggerProvider>());
    }

    [Fact]
    public void AddOpenTelemetryMetrics_WhenMetricsAreRecorded_CollectsTheFamilyAndBoundsOutboundHttp()
    {
        var exporter = new CollectingExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOpenTelemetryMetrics(CreateConfiguration(otlpEndpoint: null));
        serviceCollection.ConfigureOpenTelemetryMeterProvider(builder => builder.AddReader(reader));
        using var serviceProvider = serviceCollection.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<MeterProvider>();

        using var familyMeter = new Meter($"NCode.Identity.Tests.{Guid.NewGuid():N}");
        using var httpMeter = new Meter("System.Net.Http");
        using var unrelatedMeter = new Meter($"Unrelated.{Guid.NewGuid():N}");
        familyMeter.CreateCounter<long>("family.counter").Add(1);
        unrelatedMeter.CreateCounter<long>("unrelated.counter").Add(1);
        httpMeter
            .CreateHistogram<double>("http.client.request.duration")
            .Record(
                0.5,
                new KeyValuePair<string, object?>("http.request.method", "GET"),
                new KeyValuePair<string, object?>("server.address", "client.example"),
                new KeyValuePair<string, object?>("server.port", 443)
            );

        Assert.True(reader.Collect());

        Assert.Contains(exporter.Points, point => point.Name == "family.counter");
        Assert.DoesNotContain(exporter.Points, point => point.Name == "unrelated.counter");
        // The System.Net.Http meter is process-wide, so other tests' requests may add points.
        var httpPoints = exporter
            .Points.Where(point => point.Name == "http.client.request.duration")
            .ToList();
        Assert.NotEmpty(httpPoints);
        Assert.All(
            httpPoints,
            point =>
            {
                var tag = Assert.Single(point.Tags);
                Assert.Equal("http.request.method", tag.Key);
            }
        );
        Assert.Contains(httpPoints, point => point.Tags["http.request.method"] as string == "GET");
    }

    #endregion
}
