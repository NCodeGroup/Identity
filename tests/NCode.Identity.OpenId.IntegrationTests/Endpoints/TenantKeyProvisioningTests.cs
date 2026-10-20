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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Drives a host configured the way production runs it — no developer keys — from a fresh database. Every tenant is
/// provisioned with persisted keys when it is seeded, so the bootstrap administrator can obtain the first token and a
/// tenant created through the management API can sign before its first request.
/// </summary>
public class TenantKeyProvisioningTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string JwksPath = "/oauth2/jwks";

    private static string BootstrapClientSecretConfig { get; } =
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(BootstrapClientSecret));

    // Each test owns its factory: the keys are protected by the host's data-protection key ring.
    private static ProductionKeysApplicationFactory CreateStaticFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
                ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            }
        );

    private static ProductionKeysApplicationFactory CreateDynamicFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByHost,
                ["OpenId:TenantResolution:Root:Issuer"] = RootIssuer,
                ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = IssuerTemplate,
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
                ["BootstrapAdmin:TenantId"] = "root",
            }
        );

    [Fact]
    public async Task GetJwks_WithoutDeveloperKeys_PublishesAKeyForEachDefaultAlgorithm()
    {
        using var factory = CreateStaticFactory();
        using var client = factory.CreateClient();

        var keys = await GetJwksAsync(client, host: null);

        Assert.Equal(3, keys.Count);
        Assert.Equal(3, keys.Select(key => key.GetProperty("kid").GetString()).Distinct().Count());
        Assert.All(keys, key => Assert.Equal("sig", key.GetProperty("use").GetString()));

        var rs256 = Assert.Single(keys, key => key.GetProperty("alg").GetString() == "RS256");
        AssertRsaKey(rs256, modulusBits: 3072);

        var ps256 = Assert.Single(keys, key => key.GetProperty("alg").GetString() == "PS256");
        AssertRsaKey(ps256, modulusBits: 3072);

        var es256 = Assert.Single(keys, key => key.GetProperty("alg").GetString() == "ES256");
        Assert.Equal("EC", es256.GetProperty("kty").GetString());
        Assert.Equal("P-256", es256.GetProperty("crv").GetString());
    }

    [Fact]
    public async Task GetJwks_WhenProvisioningIsDisabled_PublishesOnlyTheKeysAnOperatorImports()
    {
        using var factory = new ProductionKeysApplicationFactory(
            new Dictionary<string, string?> { ["OpenId:Tenant:KeyProvisioning:Enabled"] = "false" }
        );
        using var client = factory.CreateClient();

        var keys = await GetJwksAsync(client, host: null);

        Assert.Empty(keys);
        Assert.Empty(await GetSecretIdsAsync(factory, HostApplicationFactory.TenantId));
    }

    [Fact]
    public async Task PostToken_OnAColdStartWithoutDeveloperKeys_IssuesATokenSignedByAProvisionedKey()
    {
        using var factory = CreateStaticFactory();
        using var client = factory.CreateClient();

        // Nothing is seeded: this one request provisions the tenant, its keys, and the bootstrap administrator.
        var accessToken = await GetBootstrapTokenAsync(client);

        using var header = DecodeJwtHeader(accessToken);
        var kid = header.RootElement.GetProperty("kid").GetString();
        var alg = header.RootElement.GetProperty("alg").GetString();

        var keys = await GetJwksAsync(client, host: null);
        var signingKey = Assert.Single(keys, key => key.GetProperty("kid").GetString() == kid);
        Assert.Equal(alg, signingKey.GetProperty("alg").GetString());
    }

    [Fact]
    public async Task SeedAsync_WhenTheTenantAlreadyHoldsItsKeys_ProvisionsNoMore()
    {
        using var factory = CreateStaticFactory();
        using var client = factory.CreateClient();
        await GetJwksAsync(client, host: null);
        var provisioned = await GetSecretIdsAsync(factory, HostApplicationFactory.TenantId);

        using (var serviceScope = factory.Services.CreateScope())
        {
            await serviceScope
                .ServiceProvider.GetRequiredService<ITenantProvisioner>()
                .SeedAsync(HostApplicationFactory.TenantId, CancellationToken.None);
        }

        var reseeded = await GetSecretIdsAsync(factory, HostApplicationFactory.TenantId);
        Assert.Equal(3, provisioned.Count);
        Assert.Equal(provisioned, reseeded);
    }

    [Fact]
    public async Task CreateTenant_WithoutDeveloperKeys_ProvisionsItsKeysBeforeItIsServed()
    {
        using var factory = CreateDynamicFactory();
        using var client = factory.CreateClient();

        // The root tenant's provisioned keys sign the bootstrap administrator's first management token.
        var accessToken = await GetBootstrapTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );

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

        // Provisioned when the tenant is created, before anything requests it (ADR-0166, ADR-0169).
        var provisioned = await GetSecretIdsAsync(factory, "acme");
        Assert.Equal(3, provisioned.Count);

        var keys = await GetJwksAsync(client, host: "acme.auth.test");
        var published = keys.Select(key => key.GetProperty("kid").GetString() ?? string.Empty);
        Assert.Equal(provisioned, published.Order(StringComparer.Ordinal));
    }

    private static void AssertRsaKey(JsonElement key, int modulusBits)
    {
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        var modulus = Base64Url.DecodeFromChars(key.GetProperty("n").GetString());
        Assert.Equal(modulusBits / 8, modulus.Length);
    }

    private static async Task<List<JsonElement>> GetJwksAsync(HttpClient client, string? host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, JwksPath);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        return
        [
            .. document.RootElement.GetProperty("keys").EnumerateArray().Select(key => key.Clone()),
        ];
    }

    // The bootstrap client registers client_secret_basic, so it presents its secret with HTTP Basic authentication.
    private static async Task<string> GetBootstrapTokenAsync(HttpClient client)
    {
        var credentials = Encoding.UTF8.GetBytes($"{BootstrapClientId}:{BootstrapClientSecret}");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
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

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private static JsonDocument DecodeJwtHeader(string jwt) =>
        JsonDocument.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[0]));

    private static async Task<List<string>> GetSecretIdsAsync(
        HostApplicationFactory factory,
        string tenantId
    )
    {
        using var serviceScope = factory.Services.CreateScope();
        await using var storeManager = await serviceScope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        var secrets = await storeManager
            .GetStore<ITenantStore>()
            .GetSecretsAsync(tenantId, CancellationToken.None);
        return [.. secrets.Value.Select(secret => secret.SecretId).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// A factory whose host selects no developer keys, as a production deployment must, with the supplied configuration
    /// overlaid. The composition root reads the developer key mode while it registers services, before configuration
    /// overlays apply, so the mode is supplied as a host setting, which reaches the host's entry point.
    /// </summary>
    private sealed class ProductionKeysApplicationFactory(
        IReadOnlyDictionary<string, string?> configuration
    ) : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseSetting("DeveloperKeys:Mode", "None");
            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(configuration)
            );
        }
    }
}
