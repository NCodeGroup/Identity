#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Drives a multi-tenant (host-routed) deployment from a fresh database, the way an operator bootstraps the hosted
/// platform: the root tenant is created when a request first reaches its address, the bootstrap administrator it is
/// seeded with obtains a management token there, and a tenant created through the management API without an issuer is
/// given the one the issuer template derives, seeded, and served beneath it.
/// </summary>
public class TenantProvisioningTests
{
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string DiscoveryPath = "/.well-known/openid-configuration";

    private static ConfiguredHostApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByHost,
                ["OpenId:TenantResolution:Root:Issuer"] = RootIssuer,
                ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = IssuerTemplate,
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    Encoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = "root",
            }
        );

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    )
    {
        var client = factory.CreateClient();

        // The TestServer's own host is the root tenant's: nothing is seeded, so this request bootstraps the control
        // plane, and with it the bootstrap administrator.
        var credentials = Encoding.UTF8.GetBytes($"{BootstrapClientId}:{BootstrapClientSecret}");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                    ["scope"] = "read:clients",
                }
            ),
        };
        using var tokenResponse = await client.SendAsync(tokenRequest);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, tokenBody);

        using var token = JsonDocument.Parse(tokenBody);
        var accessToken = token.RootElement.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
        return client;
    }

    [Fact]
    public async Task CreateTenant_WithoutAnIssuer_IsServedSeededBeneathItsDerivedIssuer()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/tenants",
            new
            {
                tenantId = "acme",
                displayName = "Acme",
                isDisabled = false,
                settings = new { },
            }
        );
        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created, createBody);

        using var created = JsonDocument.Parse(createBody);
        Assert.Equal(
            "https://acme.auth.test",
            created.RootElement.GetProperty("issuer").GetString()
        );

        // Seeded as soon as it exists, without waiting for its first request (ADR-0166).
        Assert.True(await HasOpenIdResourceServerAsync(factory, "acme"));

        // Served beneath the derived issuer, through the host strategy's route to it.
        using var discoveryRequest = new HttpRequestMessage(HttpMethod.Get, DiscoveryPath);
        discoveryRequest.Headers.Host = "acme.auth.test";
        using var discoveryResponse = await client.SendAsync(discoveryRequest);
        var discoveryBody = await discoveryResponse.Content.ReadAsStringAsync();
        Assert.True(discoveryResponse.StatusCode == HttpStatusCode.OK, discoveryBody);

        using var discovery = JsonDocument.Parse(discoveryBody);
        Assert.Equal(
            "https://acme.auth.test",
            discovery.RootElement.GetProperty("issuer").GetString()
        );
        Assert.Equal(
            "https://acme.auth.test/oauth2/token",
            discovery.RootElement.GetProperty("token_endpoint").GetString()
        );
    }

    [Fact]
    public async Task CreateTenant_WithAnMtlsEndpointAlias_IsServedOnTheAliasHostAndPublishesItsAliases()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/tenants",
            new
            {
                tenantId = "acme",
                mtlsEndpointAliasAuthority = "https://acme.mtls.test",
                displayName = "Acme",
                isDisabled = false,
                settings = new { },
            }
        );
        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created, createBody);

        using var created = JsonDocument.Parse(createBody);
        Assert.Equal(
            "https://acme.mtls.test",
            created.RootElement.GetProperty("mtlsEndpointAliasAuthority").GetString()
        );

        // A request to the alias host reaches the tenant (RFC 8705 §5), and its discovery document still names the issuer
        // and mirrors the endpoints under the alias authority.
        using var discoveryRequest = new HttpRequestMessage(HttpMethod.Get, DiscoveryPath);
        discoveryRequest.Headers.Host = "acme.mtls.test";
        using var discoveryResponse = await client.SendAsync(discoveryRequest);
        var discoveryBody = await discoveryResponse.Content.ReadAsStringAsync();
        Assert.True(discoveryResponse.StatusCode == HttpStatusCode.OK, discoveryBody);

        using var discovery = JsonDocument.Parse(discoveryBody);
        Assert.Equal(
            "https://acme.auth.test",
            discovery.RootElement.GetProperty("issuer").GetString()
        );
        Assert.Equal(
            "https://acme.mtls.test/oauth2/token",
            discovery
                .RootElement.GetProperty("mtls_endpoint_aliases")
                .GetProperty("token_endpoint")
                .GetString()
        );
    }

    [Fact]
    public async Task CreateTenant_WithAnotherTenantsHostAsItsMtlsEndpointAlias_ReportsAConflict()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/tenants",
            new
            {
                tenantId = "acme",
                displayName = "Acme",
                isDisabled = false,
                settings = new { },
            }
        );
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await client.PostAsJsonAsync(
            "/api/tenants",
            new
            {
                tenantId = "globex",
                mtlsEndpointAliasAuthority = "https://acme.auth.test",
                displayName = "Globex",
                isDisabled = false,
                settings = new { },
            }
        );
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        Assert.True(secondResponse.StatusCode == HttpStatusCode.Conflict, secondBody);
        Assert.Contains("acme.auth.test", secondBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAddress_ForANewTenant_PreviewsItsDerivedIssuer()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);

        using var response = await client.GetAsync("/api/tenants/globex/address");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var address = JsonDocument.Parse(body);
        var root = address.RootElement;
        Assert.Equal("globex", root.GetProperty("tenantId").GetString());
        Assert.Equal("https://globex.auth.test", root.GetProperty("issuer").GetString());
        Assert.True(root.GetProperty("isIssuerDerived").GetBoolean());
        Assert.False(root.GetProperty("exists").GetBoolean());
        Assert.Equal(0, root.GetProperty("failures").GetArrayLength());
    }

    [Fact]
    public async Task GetAddress_ForTheRootTenantsAddress_ReportsItAsReserved()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);

        using var response = await client.GetAsync(
            $"/api/tenants/globex/address?issuer={Uri.EscapeDataString(RootIssuer)}"
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var address = JsonDocument.Parse(body);
        var failures = address.RootElement.GetProperty("failures").EnumerateArray().ToList();
        Assert.Contains(failures, failure => failure.GetProperty("isConflict").GetBoolean());
    }

    [Fact]
    public async Task CreateTenant_WithTheIdentifierOfADeletedTenant_ReportsAConflict()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await SaveTenantDirectlyAsync(factory, "initech", issuer: null);

        using var deleteResponse = await client.DeleteAsync("/api/tenants/initech");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        Assert.True(deleteResponse.StatusCode == HttpStatusCode.NoContent, deleteBody);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/tenants",
            new
            {
                tenantId = "initech",
                displayName = "Initech",
                isDisabled = false,
                settings = new { },
            }
        );
        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.Conflict, createBody);
        Assert.Contains("never reissued", createBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAddress_ForAnIssuerAnotherTenantGaveUp_ReportsAConflict()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await SaveTenantDirectlyAsync(factory, "acme", "https://acme.auth.test");
        await SaveTenantDirectlyAsync(factory, "acme", "https://login.acme.example");

        using var response = await client.GetAsync(
            $"/api/tenants/globex/address?issuer={Uri.EscapeDataString("https://acme.auth.test")}"
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var address = JsonDocument.Parse(body);
        var failures = address.RootElement.GetProperty("failures").EnumerateArray().ToList();
        Assert.Contains(
            failures,
            failure =>
                failure.GetProperty("isConflict").GetBoolean()
                && failure
                    .GetProperty("detail")
                    .GetString()!
                    .Contains("never reissued", StringComparison.Ordinal)
        );
    }

    private static async Task SaveTenantDirectlyAsync(
        HostApplicationFactory factory,
        string tenantId,
        string? issuer
    )
    {
        using var serviceScope = factory.Services.CreateScope();
        await using var storeManager = await serviceScope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, CancellationToken.None);
        if (tenant is null)
        {
            await store.AddAsync(
                new PersistedTenant
                {
                    TenantId = tenantId,
                    ConcurrencyToken = string.Empty,
                    Issuer = issuer,
                    IsDisabled = false,
                    DisplayName = tenantId,
                    Settings = new PersistedTenantSettings
                    {
                        TenantId = tenantId,
                        ConcurrencyToken = string.Empty,
                        Value = JsonSerializer.SerializeToElement(new { }),
                    },
                    Secrets = new PersistedTenantSecrets
                    {
                        TenantId = tenantId,
                        ConcurrencyToken = string.Empty,
                        Value = [],
                    },
                },
                CancellationToken.None
            );
        }
        else
        {
            tenant.Issuer = issuer;
            await store.UpdateAsync(tenant, CancellationToken.None);
        }

        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<bool> HasOpenIdResourceServerAsync(
        HostApplicationFactory factory,
        string tenantId
    )
    {
        using var serviceScope = factory.Services.CreateScope();
        var services = serviceScope.ServiceProvider;
        using var tenantScope = services
            .GetRequiredService<IAmbientTenantAccessor>()
            .BeginScope(tenantId);
        await using var storeManager = await services
            .GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        var store = storeManager.GetStore<IResourceServerStore>();
        var resourceServer = await store.GetByIdentifierOrDefaultAsync(
            OpenIdConstants.SystemResourceServerIdentifiers.OpenId,
            CancellationToken.None
        );
        return resourceServer is not null;
    }
}
