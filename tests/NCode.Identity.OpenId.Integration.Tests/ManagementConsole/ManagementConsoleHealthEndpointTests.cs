// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NCode.Identity.ManagementConsole.Host;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.ManagementConsole;

/// <summary>
/// Proves the management console's liveness and readiness probes over HTTP.
/// </summary>
public sealed class ManagementConsoleHealthEndpointTests
{
    private static Uri BrowserBaseAddress { get; } = new("https://localhost:5005/");

    private static HttpClient CreateClient(
        WebApplicationFactory<ManagementConsoleHostMarker> factory
    ) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = BrowserBaseAddress,
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    #region GetHealth Tests

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task GetHealth_WhenEveryCheckPasses_ReturnsHealthy(string path)
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetHealth_WhenAReadinessCheckFails_ReportsNotReadyButAlive()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services
                    .AddHealthChecks()
                    .AddCheck(
                        "dependency",
                        () => HealthCheckResult.Unhealthy(),
                        tags: [HealthCheckTags.Readiness]
                    )
            )
        );
        using var client = CreateClient(failingFactory);

        using var readyResponse = await client.GetAsync("/health/ready");
        using var liveResponse = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
    }

    #endregion
}
