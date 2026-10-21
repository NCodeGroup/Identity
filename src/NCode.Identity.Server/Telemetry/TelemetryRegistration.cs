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

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace NCode.Identity.Server;

/// <summary>
/// Registers the OpenTelemetry metrics pipeline for an OpenID server: the family's meters, ASP.NET Core, Kestrel,
/// outbound HTTP, and the .NET runtime, exported over OTLP when an endpoint is configured.
/// </summary>
// See ADR-0197.
[PublicAPI]
public static class TelemetryRegistration
{
    /// <summary>
    /// The configuration key that names the OTLP endpoint, as the OpenTelemetry specification defines it; the Aspire
    /// orchestrator injects it into every service it runs.
    /// </summary>
    public const string OtlpEndpointConfigurationKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // Every meter in the family namespace, so a sibling package's meter is collected without a change here.
    private const string FamilyMeters = "NCode.Identity.*";

    // The built-in meters of ASP.NET Core, Kestrel, outbound HTTP, and the runtime.
    private static string[] PlatformMeters { get; } =
    [
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics",
        "System.Net.Http",
        "System.Runtime",
    ];

    // Outbound requests reach client-registered URIs (JWKS, sector identifiers, back-channel logout), so the attributes
    // that name the peer — server.address, server.port, network.peer.address — are unbounded; only these are kept.
    private static string[] BoundedHttpClientAttributes { get; } =
    [
        "error.type",
        "http.connection.state",
        "http.request.method",
        "http.response.status_code",
        "network.protocol.version",
        "url.scheme",
    ];

    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the OpenTelemetry metrics pipeline: every <c>NCode.Identity.*</c> meter, the ASP.NET Core,
        /// Kestrel, routing, diagnostics, <c>System.Net.Http</c>, and runtime meters, and a view that keeps only bounded
        /// attributes on the outbound-HTTP instruments. The OTLP exporter is added only when
        /// <see cref="OtlpEndpointConfigurationKey"/> is configured, so a host without a collector exports nothing.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the OTLP endpoint.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        public IServiceCollection AddOpenTelemetryMetrics(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            var builder = serviceCollection
                .AddOpenTelemetry()
                .WithMetrics(metrics =>
                    metrics
                        .AddMeter(FamilyMeters)
                        .AddMeter(PlatformMeters)
                        .AddView(instrument =>
                            instrument.Meter.Name == "System.Net.Http"
                                ? new MetricStreamConfiguration
                                {
                                    TagKeys = BoundedHttpClientAttributes,
                                }
                                : null
                        )
                );

            if (!string.IsNullOrWhiteSpace(configuration[OtlpEndpointConfigurationKey]))
                builder.UseOtlpExporter();

            return serviceCollection;
        }
    }
}
