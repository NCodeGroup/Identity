// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// The management capabilities document discloses the deployment's key management services, algorithms and secret
// types, so only an authenticated management caller reads it. See ADR-1482.
public class ManagementCapabilitiesTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string CapabilitiesPath = "/api/capabilities";

    private static ConfiguredHostApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            }
        );

    #region GetCapabilities Tests

    [Fact]
    public async Task GetCapabilities_WithoutAToken_IsAnUnauthorizedProblem()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(CapabilitiesPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetCapabilities_AsTheBootstrapAdministrator_ReturnsTheDocument()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );

        using var response = await administrator.GetAsync(CapabilitiesPath);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion
}
