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

using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

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
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetHealth_WhenReadinessCheckFails_ReportsNotReadyButAlive()
    {
        using var failingFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services
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
            builder.ConfigureTestServices(services =>
                services.AddHealthChecks().AddCheck("other", () => HealthCheckResult.Unhealthy())
            )
        );
        var client = failingFactory.CreateClient();

        using var readyResponse = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
    }
}
