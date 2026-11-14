// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
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

    // The explicit AllowedHosts of the strict posture admits the host the clients' base addresses name.
    private static Dictionary<string, string?> StrictSettings { get; } =
        new() { ["AllowedHosts"] = "op.integration.test" };

    [Fact]
    public async Task GetHealth_OverHttpsWithoutLocalDevelopment_SendsStrictTransportSecurity()
    {
        using var factory = new StrictPostureHostApplicationFactory(StrictSettings);
        using var client = CreateClient(factory, HttpsBaseAddress);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "max-age=31536000; includeSubDomains",
            Assert.Single(response.Headers.GetValues(HeaderName))
        );
    }

    [Fact]
    public async Task GetHealth_WithLocalDevelopment_SendsNoStrictTransportSecurity()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory, HttpsBaseAddress);

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains(HeaderName));
    }

    [Fact]
    public async Task GetHealth_OverHttpWithoutLocalDevelopment_SendsNoStrictTransportSecurity()
    {
        using var factory = new StrictPostureHostApplicationFactory(StrictSettings);
        using var client = CreateClient(factory, HttpBaseAddress);

        using var response = await client.GetAsync("/health/live");

        Assert.False(response.Headers.Contains(HeaderName));
    }

    [Fact]
    public async Task GetHealth_ForwardedAsHttpsByATrustedProxy_SendsStrictTransportSecurity()
    {
        using var factory = new StrictPostureHostApplicationFactory(
            new Dictionary<string, string?>(StrictSettings)
            {
                ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
            }
        );
        using var client = CreateClient(factory, HttpBaseAddress);
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
        using var factory = new StrictPostureHostApplicationFactory(StrictSettings);
        using var client = CreateClient(factory, HttpBaseAddress);
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
