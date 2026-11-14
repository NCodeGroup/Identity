// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    private static WebApplicationFactory<ManagementConsoleHostMarker> CreateStaging(
        ManagementConsoleApplicationFactory factory,
        bool trustLoopback
    )
    {
        var values = new Dictionary<string, string?>
        {
            ["LocalDevelopment:Enabled"] = "false",
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
        {
            builder.UseEnvironment(StagingEnvironment);

            // The host reads the switch and the host names while it registers its services, so they are also host
            // settings.
            foreach (var (key, value) in values)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(values)
            );

            // TestServer is not Kestrel, so the transport check has no endpoint to inspect.
            builder.ConfigureTestServices(
                StrictPostureHostApplicationFactory.RemoveTransportSecurityStartupCheck
            );
        });
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
