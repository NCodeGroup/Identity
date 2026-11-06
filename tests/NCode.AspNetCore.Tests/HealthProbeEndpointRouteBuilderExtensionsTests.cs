// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NCode.Registration;
using Xunit;

namespace NCode.AspNetCore.Tests;

public class HealthProbeEndpointRouteBuilderExtensionsTests : BaseTests
{
    private static WebApplication CreateApp(
        HealthStatus readinessStatus = HealthStatus.Healthy,
        HealthStatus untaggedStatus = HealthStatus.Healthy
    )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder
            .Services.AddHealthChecks()
            .AddCheck(
                "readiness",
                () => new HealthCheckResult(readinessStatus),
                tags: [HealthCheckTags.Readiness]
            )
            .AddCheck("untagged", () => new HealthCheckResult(untaggedStatus));

        return builder.Build();
    }

    private static List<RouteEndpoint> ReadEndpoints(WebApplication app) =>
        ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

    private static RouteEndpoint FindEndpoint(WebApplication app, string endpointName) =>
        ReadEndpoints(app)
            .Single(x =>
                x.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName == endpointName
            );

    private static async Task<int> InvokeAsync(WebApplication app, RouteEndpoint endpoint)
    {
        var httpContext = new DefaultHttpContext { RequestServices = app.Services };

        await endpoint.RequestDelegate!(httpContext);

        return httpContext.Response.StatusCode;
    }

    #region MapHealthProbes Tests

    [Fact]
    public void MapHealthProbes_Always_MapsTheLivenessAndReadinessPaths()
    {
        var app = CreateApp();

        ((IEndpointRouteBuilder)app).MapHealthProbes();

        var endpoints = ReadEndpoints(app);

        Assert.Equal(2, endpoints.Count);

        var liveness = FindEndpoint(app, HealthProbeDefaults.LivenessEndpointName);
        Assert.Equal(HealthProbeDefaults.LivenessPath, liveness.RoutePattern.RawText);

        var readiness = FindEndpoint(app, HealthProbeDefaults.ReadinessEndpointName);
        Assert.Equal(HealthProbeDefaults.ReadinessPath, readiness.RoutePattern.RawText);
    }

    [Fact]
    public async Task MapHealthProbes_WhenAReadinessCheckFails_AnswersReadyUnhealthyAndLiveHealthy()
    {
        var app = CreateApp(readinessStatus: HealthStatus.Unhealthy);

        ((IEndpointRouteBuilder)app).MapHealthProbes();

        var readyStatusCode = await InvokeAsync(
            app,
            FindEndpoint(app, HealthProbeDefaults.ReadinessEndpointName)
        );
        var liveStatusCode = await InvokeAsync(
            app,
            FindEndpoint(app, HealthProbeDefaults.LivenessEndpointName)
        );

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, readyStatusCode);
        Assert.Equal(StatusCodes.Status200OK, liveStatusCode);
    }

    [Fact]
    public async Task MapHealthProbes_WhenAnUntaggedCheckFails_AnswersReadyHealthy()
    {
        var app = CreateApp(untaggedStatus: HealthStatus.Unhealthy);

        ((IEndpointRouteBuilder)app).MapHealthProbes();

        var readyStatusCode = await InvokeAsync(
            app,
            FindEndpoint(app, HealthProbeDefaults.ReadinessEndpointName)
        );

        Assert.Equal(StatusCodes.Status200OK, readyStatusCode);
    }

    [Fact]
    public void MapHealthProbes_WhenTheGroupRequiresAHost_RequiresItOnBothProbes()
    {
        var app = CreateApp();

        var group = ((IEndpointRouteBuilder)app).MapHealthProbes();
        group.RequireHost("probe.example.com");

        foreach (var endpoint in ReadEndpoints(app))
        {
            var hostMetadata = Assert.Single(endpoint.Metadata.GetOrderedMetadata<IHostMetadata>());
            Assert.Equal(["probe.example.com"], hostMetadata.Hosts);
        }
    }

    [Fact]
    public void MapHealthProbes_WhenEndpointsIsNull_Throws()
    {
        IEndpointRouteBuilder endpoints = null!;

        Assert.Throws<ArgumentNullException>(() => endpoints.MapHealthProbes());
    }

    #endregion
}
