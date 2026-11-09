// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// The management capabilities document discloses the deployment's key management services, algorithms and secret
// types, so only an authenticated management caller reads it. See ADR-1482.
public class ManagementCapabilitiesTests
{
    private const string CapabilitiesPath = "/api/capabilities";

    #region GetCapabilities Tests

    [Fact]
    public async Task GetCapabilities_WithoutAToken_IsAnUnauthorizedProblem()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(CapabilitiesPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetCapabilities_AsTheBootstrapAdministrator_ReturnsTheDocument()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync();

        using var response = await administrator.GetAsync(CapabilitiesPath);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion
}
