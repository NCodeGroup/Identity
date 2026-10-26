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
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
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
        Base64Url.EncodeToString(SecureEncoding.UTF8.GetBytes(BootstrapClientSecret));

    // The signing lists of the server's settings, each of which names the algorithms a tenant uses.
    private static string[] SigningListNames { get; } =
    [
        OpenIdSettingNames.IdTokenSigningAlgValuesSupported,
        OpenIdSettingNames.UserInfoSigningAlgValuesSupported,
        OpenIdSettingNames.IntrospectionSigningAlgValuesSupported,
        OpenIdSettingNames.AuthorizationSigningAlgValuesSupported,
        OpenIdSettingNames.AccessTokenSigningAlgValuesSupported,
    ];

    // Each test owns its factory: the keys are protected by the tenant's master key.
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
        CreateDynamicFactory(new Dictionary<string, string?>());

    // The server settings are configuration keys beneath OpenId:Server:Settings, merged into the dynamic configuration.
    private static ProductionKeysApplicationFactory CreateDynamicFactory(
        IReadOnlyDictionary<string, string?> serverSettings
    )
    {
        var configuration = new Dictionary<string, string?>
        {
            ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                .TenantStrategyCodes
                .DynamicByHost,
            ["OpenId:TenantResolution:Root:Issuer"] = RootIssuer,
            ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = IssuerTemplate,
            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
            ["BootstrapAdmin:TenantId"] = "root",
        };
        foreach (var (key, value) in serverSettings)
        {
            configuration[key] = value;
        }

        return new ProductionKeysApplicationFactory(configuration);
    }

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

    [Fact]
    public async Task CreateTenant_WhenItsProfileExcludesAnAlgorithm_ProvisionsNoKeyForIt()
    {
        using var factory = CreateDynamicFactory();
        using var client = factory.CreateClient();

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
                settings = new Dictionary<string, object>
                {
                    ["security_profiles"] = (string[])[OpenIdConstants.SecurityProfiles.Fapi2],
                },
            }
        );
        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created, createBody);

        // FAPI 2.0 permits PS256 and ES256 only, so the tenant holds and publishes no RS256 key (ADR-0169 decision 9).
        var keys = await GetJwksAsync(client, host: "acme.auth.test");
        Assert.Equal(
            ["ES256", "PS256"],
            keys.Select(key => key.GetProperty("alg").GetString()).Order(StringComparer.Ordinal)
        );
        Assert.Equal(2, (await GetSecretIdsAsync(factory, "acme")).Count);
    }

    [Fact]
    public async Task UpdateServerSettings_WhenTheChangePermitsAConfiguredAlgorithm_ProvisionsItsKeyForEveryTenant()
    {
        using var factory = CreateDynamicFactory();
        using var client = factory.CreateClient();

        var accessToken = await GetBootstrapTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );

        // The server stops permitting ES256, so a tenant created now is provisioned without its key.
        await PatchServerSettingsAsync(client, operation: "add", value: ["RS256", "PS256"]);

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
        Assert.Equal(2, (await GetSecretIdsAsync(factory, "acme")).Count);

        // Restoring the default lists permits ES256 again; the reconciliation of the server's settings seeds it for the
        // tenant (ADR-0219), so the key arrives after the PATCH returns.
        await PatchServerSettingsAsync(client, operation: "remove", value: null);

        var secretIds = await WaitForSecretIdsAsync(factory, "acme", count: 3);
        Assert.Equal(3, secretIds.Count);
    }

    [Fact]
    public async Task ReloadServerConfiguration_WhenItPermitsAConfiguredAlgorithm_ProvisionsItsKeyForEveryTenant()
    {
        // The server's configuration lists RS256 and PS256 only, so a tenant created now is provisioned without ES256.
        var serverSettings = new Dictionary<string, string?>();
        foreach (var name in SigningListNames)
        {
            serverSettings[$"OpenId:Server:Settings:{name}:0"] = "RS256";
            serverSettings[$"OpenId:Server:Settings:{name}:1"] = "PS256";
        }

        using var factory = CreateDynamicFactory(serverSettings);
        using var client = factory.CreateClient();

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
        Assert.Equal(2, (await GetSecretIdsAsync(factory, "acme")).Count);

        // A configuration reload that adds ES256 to every list permits it; the reconciliation of the server's settings
        // seeds the key for the tenant (ADR-0219), though nothing was written through the management API.
        var configuration = (IConfigurationRoot)
            factory.Services.GetRequiredService<IConfiguration>();
        foreach (var name in SigningListNames)
        {
            configuration[$"OpenId:Server:Settings:{name}:2"] = "ES256";
        }

        configuration.Reload();

        var secretIds = await WaitForSecretIdsAsync(factory, "acme", count: 3);
        Assert.Equal(3, secretIds.Count);
    }

    [Fact]
    public async Task DeleteTenant_WithProvisionedKeys_DeletesItWithItsKeys()
    {
        using var factory = CreateDynamicFactory();
        using var client = factory.CreateClient();

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
        Assert.Equal(3, (await GetSecretIdsAsync(factory, "acme")).Count);

        // The tenant's keys are part of it, so they no longer block its deletion (ADR-0169).
        using var deleteResponse = await client.DeleteAsync("/api/tenants/acme");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        Assert.True(deleteResponse.StatusCode == HttpStatusCode.NoContent, deleteBody);

        using var getResponse = await client.GetAsync("/api/tenants/acme");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.True(getResponse.StatusCode == HttpStatusCode.NotFound, getBody);
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
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
        );
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

    // One operation per signing list of the server's settings: an "add" replaces the list with the value, a "remove"
    // drops it so the default applies again.
    private static async Task PatchServerSettingsAsync(
        HttpClient client,
        string operation,
        string[]? value
    )
    {
        var operations = SigningListNames.Select(name =>
            value is null
                ? (object)new { op = operation, path = $"/{name}" }
                : new
                {
                    op = operation,
                    path = $"/{name}",
                    value,
                }
        );

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/servers/{OpenIdServerOptions.DefaultServerId}/settings"
        )
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);
    }

    // The reconciliation of the server's settings runs in the background, so the keys of the other tenants arrive after
    // the change that permits them returns.
    private static async Task<List<string>> WaitForSecretIdsAsync(
        HostApplicationFactory factory,
        string tenantId,
        int count
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var secretIds = await GetSecretIdsAsync(factory, tenantId);
        while (secretIds.Count < count && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            secretIds = await GetSecretIdsAsync(factory, tenantId);
        }

        return secretIds;
    }

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
