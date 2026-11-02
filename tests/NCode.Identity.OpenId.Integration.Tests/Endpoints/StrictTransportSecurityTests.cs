// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public sealed class StrictTransportSecurityTests
{
    private const string HeaderName = "Strict-Transport-Security";

    private const string HttpsBaseAddress = "https://op.integration.test/";

    private const string HttpBaseAddress = "http://op.integration.test/";

    // Qualified because the NCode.Identity.OpenId.Environments namespace shadows the framework's Environments class.
    private static string StagingEnvironment { get; } =
        Microsoft.Extensions.Hosting.Environments.Staging;

    [Fact]
    public async Task GetHealth_OverHttpsOutsideDevelopment_SendsStrictTransportSecurity()
    {
        using var factory = new HostApplicationFactory();
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
        using var client = CreateClient(staging, HttpsBaseAddress);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "max-age=31536000; includeSubDomains",
            Assert.Single(response.Headers.GetValues(HeaderName))
        );
    }

    [Fact]
    public async Task GetHealth_InDevelopment_SendsNoStrictTransportSecurity()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory, HttpsBaseAddress);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains(HeaderName));
    }

    [Fact]
    public async Task GetHealth_OverHttpOutsideDevelopment_SendsNoStrictTransportSecurity()
    {
        using var factory = new HostApplicationFactory();
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
        using var client = CreateClient(staging, HttpBaseAddress);

        using var response = await client.GetAsync("/health/live");

        Assert.False(response.Headers.Contains(HeaderName));
    }

    [Fact]
    public async Task GetHealth_ForwardedAsHttpsByATrustedProxy_SendsStrictTransportSecurity()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = "127.0.0.1" }
        );
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
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
        using var factory = new HostApplicationFactory();
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
        using var client = CreateClient(staging, HttpBaseAddress);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains(HeaderName));
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<HostApiMarker> factory,
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
