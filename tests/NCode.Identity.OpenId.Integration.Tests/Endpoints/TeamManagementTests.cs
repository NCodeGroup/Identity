// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives teams through the management API of a multi-tenant (host-routed) deployment: a team holds tenants, its settings
/// document sits between the platform's and its tenants' and reaches them at once, disabling it silences them, it caps
/// how many it holds, it cannot be deleted while it holds any, and its usage lists each tenant's counts.
/// </summary>
public class TeamManagementTests
{
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string DiscoveryPath = "/.well-known/openid-configuration";

    #region Scaffolding

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
                    SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = "root",
            }
        );

    private static Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    ) => factory.CreateManagementClientAsync(BootstrapClientId, BootstrapClientSecret);

    private static async Task CreateTeamAsync(HttpClient client, string teamId, object settings)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                teamId,
                displayName = teamId,
                isDisabled = false,
                settings,
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    private static async Task<HttpResponseMessage> PostTenantAsync(
        HttpClient client,
        string teamId,
        string tenantId
    ) =>
        await client.PostAsJsonAsync(
            $"/api/teams/{teamId}/tenants",
            new
            {
                tenantId,
                displayName = tenantId,
                isDisabled = false,
                settings = new { },
            }
        );

    private static async Task CreateTenantAsync(HttpClient client, string teamId, string tenantId)
    {
        using var response = await PostTenantAsync(client, teamId, tenantId);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    private static async Task<HttpResponseMessage> GetDiscoveryAsync(
        HttpClient client,
        string tenantId
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DiscoveryPath);
        request.Headers.Host = $"{tenantId}.auth.test";
        return await client.SendAsync(request);
    }

    private static async Task PatchAsync(HttpClient client, string path, object operations)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
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

    private static Task SetTeamDisabledAsync(HttpClient client, string teamId, bool isDisabled) =>
        PatchAsync(
            client,
            $"/api/teams/{teamId}",
            new[]
            {
                new
                {
                    op = "replace",
                    path = "/isDisabled",
                    value = isDisabled,
                },
            }
        );

    private static async Task<List<string>> GetIdTokenSigningAlgorithmsAsync(
        HttpClient client,
        string tenantId
    )
    {
        using var response = await GetDiscoveryAsync(client, tenantId);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        return JsonElement
            .Parse(body)
            .GetProperty(OpenIdSettingNames.IdTokenSigningAlgValuesSupported)
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToList();
    }

    private static long GetCount(JsonElement usage, string tenantId) =>
        usage
            .GetProperty("entries")
            .EnumerateArray()
            .Where(entry =>
                entry.GetProperty("tenantId").GetString() == tenantId
                && entry.GetProperty("meter").GetString() == TenantUsageMeters.Requests
            )
            .Sum(entry => entry.GetProperty("count").GetInt64());

    #endregion

    #region CreateTeam Tests

    [Fact]
    public async Task CreateTeam_ThenCreateItsTenant_ServesTheTenantUnderTheTeamsSettings()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);

        await CreateTeamAsync(client, "acme", new { environment_type = "development" });
        await CreateTenantAsync(client, "acme", "acme-dev");

        using var response = await client.GetAsync("/api/tenants/acme-dev/effective-settings");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var settings = JsonElement.Parse(body);
        Assert.Equal(
            "development",
            settings.GetProperty("settings").GetProperty("environment_type").GetString()
        );
    }

    #endregion

    #region PatchTeamSettings Tests

    [Fact]
    public async Task PatchTeamSettings_ReachesTheTeamsTenantsAtOnce()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(client, "globex", new { });
        await CreateTenantAsync(client, "globex", "globex-dev");

        var before = await GetIdTokenSigningAlgorithmsAsync(client, "globex-dev");
        Assert.Contains("PS256", before);

        await PatchAsync(
            client,
            "/api/teams/globex/settings",
            new[]
            {
                new
                {
                    op = "add",
                    path = $"/{OpenIdSettingNames.IdTokenSigningAlgValuesSupported}",
                    value = new[] { "RS256" },
                },
            }
        );

        // No polling: the change is served as soon as the write returns.
        var after = await GetIdTokenSigningAlgorithmsAsync(client, "globex-dev");
        Assert.Equal<string>(["RS256"], after);
    }

    #endregion

    #region UpdateTeam Tests

    [Fact]
    public async Task DisableTeam_MakesItsTenantsAnswerNotFound()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(client, "initech", new { });
        await CreateTenantAsync(client, "initech", "initech-dev");

        using (var served = await GetDiscoveryAsync(client, "initech-dev"))
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        await SetTeamDisabledAsync(client, "initech", isDisabled: true);

        using (var silenced = await GetDiscoveryAsync(client, "initech-dev"))
            Assert.Equal(HttpStatusCode.NotFound, silenced.StatusCode);

        await SetTeamDisabledAsync(client, "initech", isDisabled: false);

        using (var restored = await GetDiscoveryAsync(client, "initech-dev"))
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    #endregion

    #region CreateTenant Tests

    [Fact]
    public async Task CreateTenant_WhenTheTeamIsFull_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(client, "hooli", new { max_tenants = 1 });
        await CreateTenantAsync(client, "hooli", "hooli-one");

        using var response = await PostTenantAsync(client, "hooli", "hooli-two");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    #endregion

    #region DeleteTeam Tests

    [Fact]
    public async Task DeleteTeam_WhileItHasTenants_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(client, "umbrella", new { });
        await CreateTenantAsync(client, "umbrella", "umbrella-dev");

        using var response = await client.DeleteAsync("/api/teams/umbrella");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    #endregion

    #region GetTeamUsage Tests

    [Fact]
    public async Task GetTeamUsage_AfterAdmittedRequests_ListsEachTenantsCounts()
    {
        using var host = CreateFactory();
        using var recordingHost = host.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<TenantUsageOptions>(options =>
                    options.FlushInterval = TimeSpan.FromMilliseconds(50)
                )
            )
        );
        using var administrator = await CreateAdministratorClientAsync(host);
        await CreateTenantAsync(administrator, HostApplicationFactory.TeamId, "usage-one");
        await CreateTenantAsync(administrator, HostApplicationFactory.TeamId, "usage-two");

        var client = recordingHost.CreateClient();
        foreach (var tenantId in new[] { "usage-one", "usage-two" })
        {
            for (var i = 0; i < 2; ++i)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
                {
                    Content = new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                        }
                    ),
                };
                request.Headers.Host = $"{tenantId}.auth.test";
                using var response = await client.SendAsync(request);
                Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
            }
        }

        // The ledger is written by the periodic flush, so poll until both tenants' counts arrive.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        JsonElement usage;
        while (true)
        {
            using var response = await administrator.GetAsync(
                $"/api/teams/{HostApplicationFactory.TeamId}/usage"
            );
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            usage = JsonElement.Parse(body);
            if (
                (GetCount(usage, "usage-one") >= 2 && GetCount(usage, "usage-two") >= 2)
                || DateTime.UtcNow > deadline
            )
                break;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(HostApplicationFactory.TeamId, usage.GetProperty("teamId").GetString());
        Assert.True(GetCount(usage, "usage-one") >= 2, usage.ToString());
        Assert.True(GetCount(usage, "usage-two") >= 2, usage.ToString());
    }

    #endregion
}
