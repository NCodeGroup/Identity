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

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Proves that the tenant route the tenant-resolution configuration declares is the route the tenant is actually served
/// under: its protocol endpoints, and the discovery document whose issuer OpenID Connect Discovery requires to be the
/// URL the document is fetched beneath.
/// </summary>
public class TenantRouteTests
{
    private const string DiscoveryPath = "/.well-known/openid-configuration";

    private static async Task<JsonElement> GetDiscoveryAsync(
        HostApplicationFactory factory,
        string discoveryPath
    )
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(discoveryPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        return JsonElement.Parse(json);
    }

    #region StaticSingle Tests

    [Fact]
    public async Task GetDiscovery_WhenStaticTenantPathConfigured_IsServedBeneathIt()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StaticSingle:TenantPath"] = "/identity",
            }
        );

        var discovery = await GetDiscoveryAsync(factory, $"/identity{DiscoveryPath}");

        Assert.Equal("http://localhost/identity", discovery.GetProperty("issuer").GetString());
        Assert.Equal(
            "http://localhost/identity/oauth2/token",
            discovery.GetProperty("token_endpoint").GetString()
        );
        Assert.Equal(
            "http://localhost/identity/oauth2/jwks",
            discovery.GetProperty("jwks_uri").GetString()
        );
    }

    [Fact]
    public async Task GetDiscovery_WhenStaticTenantPathConfigured_IsNotServedAtTheRoot()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StaticSingle:TenantPath"] = "/identity",
            }
        );
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(DiscoveryPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDiscovery_WhenIssuerIsPinnedBeneathTheTenantPath_PublishesEndpointsBeneathTheIssuer()
    {
        const string issuer = "https://op.example/identity";

        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StaticSingle:TenantPath"] = "/identity",
                ["OpenId:TenantResolution:StaticSingle:Issuer"] = issuer,
            }
        );

        var discovery = await GetDiscoveryAsync(factory, $"/identity{DiscoveryPath}");

        // The issuer already names the tenant route, so the published endpoints must not repeat it.
        Assert.Equal(issuer, discovery.GetProperty("issuer").GetString());
        Assert.Equal($"{issuer}/oauth2/token", discovery.GetProperty("token_endpoint").GetString());
    }

    #endregion

    #region DynamicByPath Tests

    [Fact]
    public async Task GetDiscovery_WhenTenantIsSelectedByPath_IsServedBeneathTheTenantsRoute()
    {
        const string tenantId = "acme";

        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByPath,
            }
        );
        await SeedTenantAsync(factory, tenantId);

        var discovery = await GetDiscoveryAsync(factory, $"/{tenantId}{DiscoveryPath}");

        Assert.Equal("http://localhost/acme", discovery.GetProperty("issuer").GetString());
        Assert.Equal(
            "http://localhost/acme/oauth2/token",
            discovery.GetProperty("token_endpoint").GetString()
        );
    }

    [Fact]
    public async Task GetDiscovery_WhenTenantIsSelectedByPathAndUnknown_ReturnsNotFound()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByPath,
            }
        );
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/unknown{DiscoveryPath}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task SeedTenantAsync(HostApplicationFactory factory, string tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );

        var store = storeManager.GetStore<ITenantStore>();
        await store.AddAsync(
            new PersistedTenant
            {
                TenantId = tenantId,
                Issuer = null,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                IsDisabled = false,
                DisplayName = tenantId,
                Settings = new PersistedTenantSettings
                {
                    TenantId = tenantId,
                    ConcurrencyToken = Guid.NewGuid().ToString("N"),
                    Value = JsonSerializer.SerializeToElement(null, typeof(object)),
                },
                Secrets = new PersistedTenantSecrets
                {
                    TenantId = tenantId,
                    ConcurrencyToken = Guid.NewGuid().ToString("N"),
                    Value = [],
                },
            },
            CancellationToken.None
        );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    #endregion
}
