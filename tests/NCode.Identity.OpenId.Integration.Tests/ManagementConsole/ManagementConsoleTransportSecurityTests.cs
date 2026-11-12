// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NCode.Identity.ManagementConsole.Host;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.ManagementConsole;

public sealed class ManagementConsoleTransportSecurityTests
{
    private const string HeaderName = "Strict-Transport-Security";

    private const string HttpsBaseAddress = "https://console.integration.test/";

    private const string HttpBaseAddress = "http://console.integration.test/";

    // Qualified because the NCode.Identity.OpenId.Environments namespace shadows the framework's Environments class.
    private static string StagingEnvironment { get; } =
        Microsoft.Extensions.Hosting.Environments.Staging;

    private static string ProductionEnvironment { get; } =
        Microsoft.Extensions.Hosting.Environments.Production;

    [Fact]
    public async Task GetHealth_OverHttpsOutsideDevelopment_SendsStrictTransportSecurity()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        await using var staging = CreateStaging(factory, trustLoopback: false);
        using var client = CreateClient(staging, HttpsBaseAddress);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "max-age=31536000; includeSubDomains",
            Assert.Single(response.Headers.GetValues(HeaderName))
        );
    }

    [Fact]
    public async Task GetHealth_ForwardedAsHttpsByATrustedProxy_SendsStrictTransportSecurity()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        await using var staging = CreateStaging(factory, trustLoopback: true);
        using var client = CreateClient(staging, HttpBaseAddress);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "max-age=31536000; includeSubDomains",
            Assert.Single(response.Headers.GetValues(HeaderName))
        );
    }

    [Fact]
    public async Task GetHealth_ForwardedAsHttpsWithoutATrustedProxy_SendsNoStrictTransportSecurity()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        await using var staging = CreateStaging(factory, trustLoopback: false);
        using var client = CreateClient(staging, HttpBaseAddress);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains(HeaderName));
    }

    [Fact]
    public async Task CreateClient_InProductionOnAServerThatIsNotKestrel_RefusesToStart()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        await using var production = CreateHost(
            factory,
            ProductionEnvironment,
            trustLoopback: false
        );

        var exception = Record.Exception(() => production.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(
            "not Kestrel",
            string.Join(Environment.NewLine, Flatten(exception).Select(inner => inner.Message))
        );
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static WebApplicationFactory<ManagementConsoleHostMarker> CreateStaging(
        ManagementConsoleApplicationFactory factory,
        bool trustLoopback
    ) => CreateHost(factory, StagingEnvironment, trustLoopback);

    private static WebApplicationFactory<ManagementConsoleHostMarker> CreateHost(
        ManagementConsoleApplicationFactory factory,
        string environmentName,
        bool trustLoopback
    )
    {
        var values = new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "console.integration.test",
            ["Bff:Frontends:mgmt:Authority"] = "https://id.integration.test",
            ["Bff:Frontends:mgmt:PublicOrigin"] = "https://console.integration.test",
            ["ManagementConsole:ManagementApiUrl"] = "https://id.integration.test/api",
        };

        if (trustLoopback)
        {
            values["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1";
        }

        return factory.WithWebHostBuilder(builder =>
            builder
                .UseEnvironment(environmentName)
                .ConfigureAppConfiguration(configurationBuilder =>
                    configurationBuilder.AddInMemoryCollection(values)
                )
        );
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<ManagementConsoleHostMarker> factory,
        string baseAddress
    ) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri(baseAddress),
                AllowAutoRedirect = false,
            }
        );
}
