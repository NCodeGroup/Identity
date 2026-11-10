// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class HealthEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task GetHealth_WhenEveryCheckPasses_ReturnsHealthy(string path)
    {
        var client = Factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task GetHealth_WhenReadinessCheckFails_ReportsNotReadyButAlive()
    {
        using var failingFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection
                    .AddHealthChecks()
                    .AddCheck("dependency", () => HealthCheckResult.Unhealthy(), tags: ["ready"])
            )
        );
        var client = failingFactory.CreateClient();

        using var readyResponse = await client.GetAsync("/health/ready");
        using var liveResponse = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
    }

    [Fact]
    public async Task GetHealth_WhenUntaggedCheckFails_StaysReady()
    {
        using var failingFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection
                    .AddHealthChecks()
                    .AddCheck("other", () => HealthCheckResult.Unhealthy())
            )
        );
        var client = failingFactory.CreateClient();

        using var readyResponse = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
    }
}
