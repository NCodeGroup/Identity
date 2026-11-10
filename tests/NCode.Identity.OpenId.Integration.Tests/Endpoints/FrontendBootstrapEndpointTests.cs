// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Pins the frontend bootstrap document to the tenant and host that serve the surface: the bundle trusts that
/// authority and sends its access token to that API base, so the serving request's <c>Host</c> changes neither.
/// </summary>
// See ADR-1182 and ADR-1312.
public class FrontendBootstrapEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task GetBootstrap_WithAnotherHost_PublishesTheTenantsIssuerAndARootRelativeApiBaseUncached()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var request = new HttpRequestMessage(HttpMethod.Get, "/config/bootstrap.json");
        request.Headers.Host = "attacker.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            HostApplicationFactory.TestServerIssuer,
            jsonElement.GetProperty("issuer").GetString()
        );
        Assert.Equal("/", jsonElement.GetProperty("apiBaseUrl").GetString());
    }
}
