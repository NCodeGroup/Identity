// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Pins the frontend bootstrap document's issuer to the tenant that serves the surface: the bundle trusts that
/// authority, so the serving request's <c>Host</c> never changes it (ADR-1182).
/// </summary>
public class FrontendBootstrapEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task GetBootstrap_WithAnotherHost_PublishesTheTenantsIssuerAndForbidsCaching()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var request = new HttpRequestMessage(HttpMethod.Get, "/config/bootstrap.json");
        request.Headers.Host = "attacker.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            HostApplicationFactory.TestServerIssuer,
            jsonElement.GetProperty("issuer").GetString()
        );
    }
}
