// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives the management API under each dynamic tenant strategy with a token the root (control-plane) tenant issued:
/// the API authenticates the caller against the root tenant whatever tenant the request's host or path addresses, and
/// the route's <c>tenantId</c> only scopes the data. The static strategy, whose own tenant is the
/// control-plane tenant, is driven by every other management integration test.
/// </summary>
// See ADR-1191.
public class ManagementControlPlaneTests
{
    private const string HostIssuerTemplate = "https://{tenantId}.auth.test";
    private const string PathIssuerTemplate = "http://localhost/{tenantId}";

    #region Helpers

    private static BootstrapAdminApplicationFactory CreateFactory(
        Dictionary<string, string?> strategySettings
    ) => new(strategySettings, "root");

    private static BootstrapAdminApplicationFactory CreateHostFactory() =>
        CreateFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByHost,
                ["OpenId:TenantResolution:Root:Issuer"] = "http://localhost",
                ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = HostIssuerTemplate,
            }
        );

    private static BootstrapAdminApplicationFactory CreatePathFactory() =>
        CreateFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByPath,
                ["OpenId:TenantResolution:DynamicByPath:IssuerTemplate"] = PathIssuerTemplate,
            }
        );

    private static async ValueTask<JsonElement> CreateTenantAsync(
        HttpClient client,
        string tenantId,
        string? host,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/teams/{HostApplicationFactory.TeamId}/tenants"
        )
        {
            Content = JsonContent.Create(
                new
                {
                    tenantId,
                    displayName = tenantId,
                    isDisabled = false,
                    settings = new { },
                }
            ),
        };
        if (host is not null)
            request.Headers.Host = host;

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        var jsonElement = JsonElement.Parse(body);
        return jsonElement;
    }

    #endregion

    #region DynamicByHost Tests

    [Fact]
    public async Task CreateTenant_WhenReachedOnAHostThatIsNoTenants_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreateHostFactory();
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);

        var created = await CreateTenantAsync(
            client,
            "acme",
            host: "manage.auth.test",
            CancellationToken.None
        );

        Assert.Equal("https://acme.auth.test", created.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task ListClients_WhenReachedOnAnotherTenantsHost_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreateHostFactory();
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
        await CreateTenantAsync(client, "acme", host: null, CancellationToken.None);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenants/acme/clients");
        request.Headers.Host = "acme.auth.test";
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion

    #region DynamicByPath Tests

    [Fact]
    public async Task CreateTenant_WhenTheStrategyIsByPath_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreatePathFactory();
        using var client = await factory.CreateAdministratorClientAsync(
            "/root/oauth2/token",
            CancellationToken.None
        );

        var created = await CreateTenantAsync(client, "acme", host: null, CancellationToken.None);

        Assert.Equal("http://localhost/acme", created.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task ListClients_WhenTheStrategyIsByPath_AuthenticatesAgainstTheRootTenant()
    {
        using var factory = CreatePathFactory();
        using var client = await factory.CreateAdministratorClientAsync(
            "/root/oauth2/token",
            CancellationToken.None
        );
        await CreateTenantAsync(client, "acme", host: null, CancellationToken.None);

        using var response = await client.GetAsync("/api/tenants/acme/clients");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion
}
