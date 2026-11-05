// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Persistence.Stores;
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
    private const string RootTenantId = "root";
    private const string ConsoleClientId = "console";
    private const string ConsoleRedirectUri = "https://console.test/callback";
    private const string UserName = "team-user";
    private const string Password = "team-user-password";
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

    // Seeds an end-user of the root tenant (a local account with a password, its principal and its self-issued identity)
    // and the public console client, grants the user the role at the team, and returns a client that calls the
    // management API with the user's access token.
    private static async Task<HttpClient> CreateTeamUserClientAsync(
        HostApplicationFactory factory,
        HttpClient administrator,
        string teamId,
        string grantPath
    )
    {
        var principalId = await SeedConsoleUserAsync(factory);

        using var grant = await administrator.PostAsJsonAsync(
            $"/api/teams/{teamId}/{grantPath}",
            new { principalId }
        );
        var body = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Created, body);

        return await factory.CreateManagementUserClientAsync(
            ConsoleClientId,
            ConsoleRedirectUri,
            UserName,
            Password,
            "openid read:clients"
        );
    }

    private static async Task<string> SeedConsoleUserAsync(HostApplicationFactory factory)
    {
        var localAccountId = Guid.NewGuid().ToString("N");
        var principalId = $"pid-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services
            .GetRequiredService<IAmbientTenantAccessor>()
            .BeginScope(RootTenantId);
        await using var storeManager = await services
            .GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);

        await storeManager
            .GetStore<ILocalAccountStore>()
            .AddAsync(
                new PersistedLocalAccount
                {
                    TenantId = RootTenantId,
                    LocalAccountId = localAccountId,
                    UserName = UserName,
                    Email = null,
                    EmailVerified = false,
                    IsEnabled = true,
                    Claims = [],
                    ConcurrencyToken = string.Empty,
                },
                SecureEncoding.UTF8.GetBytes(Password),
                CancellationToken.None
            );
        await storeManager
            .GetStore<IFederatedPrincipalStore>()
            .AddAsync(
                new PersistedFederatedPrincipal
                {
                    TenantId = RootTenantId,
                    PrincipalId = principalId,
                    ConcurrencyToken = string.Empty,
                },
                CancellationToken.None
            );
        await storeManager
            .GetStore<IFederatedIdentityStore>()
            .AddAsync(
                new PersistedFederatedIdentity
                {
                    TenantId = RootTenantId,
                    FederatedIdentityId = Guid.NewGuid().ToString("N"),
                    PrincipalId = principalId,
                    Issuer = AccountConstants.SelfIssuer,
                    Subject = localAccountId,
                    JoinKey = null,
                    ConcurrencyToken = string.Empty,
                },
                CancellationToken.None
            );
        await storeManager
            .GetStore<IClientStore>()
            .AddAsync(
                new PersistedClient
                {
                    TenantId = RootTenantId,
                    ClientId = ConsoleClientId,
                    IsDisabled = false,
                    Settings = new PersistedClientSettings
                    {
                        TenantId = RootTenantId,
                        ClientId = ConsoleClientId,
                        Value = JsonSerializer.SerializeToElement(
                            new Dictionary<string, object>
                            {
                                ["redirect_uris"] = new[] { ConsoleRedirectUri },
                                ["token_endpoint_auth_method"] = OpenIdProtocol
                                    .ClientAuthenticationMethods
                                    .None,
                            }
                        ),
                    },
                    Secrets = new PersistedClientSecrets
                    {
                        TenantId = RootTenantId,
                        ClientId = ConsoleClientId,
                        Value = [],
                    },
                },
                CancellationToken.None
            );
        await storeManager.SaveChangesAsync(CancellationToken.None);

        return principalId;
    }

    // Grants the principal the role at the platform node, whose authority reaches every team and tenant.
    private static async Task GrantPlatformRoleAsync(
        HostApplicationFactory factory,
        string principalId,
        string roleName
    )
    {
        using var scope = factory.Services.CreateScope();
        await using var storeManager = await scope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);

        await storeManager
            .GetStore<IRoleAssignmentStore>()
            .AddAsync(
                new PersistedRoleAssignment
                {
                    TenantId = RootTenantId,
                    AssignmentId = Guid.NewGuid().ToString("N"),
                    PrincipalId = principalId,
                    RoleName = roleName,
                    ResourceType = OpenIdResourceTypes.Platform,
                    ResourceId = OpenIdResourceIds.Platform,
                    ConcurrencyToken = string.Empty,
                },
                CancellationToken.None
            );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<HttpResponseMessage> SendPatchAsync(
        HttpClient client,
        string path,
        object operations
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };
        return await client.SendAsync(request);
    }

    // Creates a client that authenticates with a client secret in the tenant and returns its credentials.
    private static async Task<(string ClientId, string ClientSecret)> CreateSecretClientAsync(
        HttpClient client,
        string tenantId
    )
    {
        using var created = await client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                },
            }
        );
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
        var clientId = JsonElement.Parse(createdBody).GetProperty("clientId").GetString();
        Assert.NotNull(clientId);

        using var secret = await client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var secretBody = await secret.Content.ReadAsStringAsync();
        Assert.True(secret.StatusCode == HttpStatusCode.Created, secretBody);
        var material = JsonElement.Parse(secretBody).GetProperty("secretMaterial").GetString();
        Assert.NotNull(material);
        return (clientId, material);
    }

    // Creates a client in the control-plane tenant that authenticates with HTTP Basic and returns its credentials and
    // the principal that its tokens resolve to.
    private static async Task<(
        string ClientId,
        string ClientSecret,
        string PrincipalId
    )> CreateMachineClientAsync(HttpClient administrator)
    {
        using var created = await administrator.PostAsJsonAsync(
            $"/api/tenants/{RootTenantId}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretBasic,
                },
            }
        );
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
        var createdClient = JsonElement.Parse(createdBody);
        var clientId = createdClient.GetProperty("clientId").GetString();
        Assert.NotNull(clientId);
        var principalId = createdClient.GetProperty("principalId").GetString();
        Assert.NotNull(principalId);

        using var secret = await administrator.PostAsJsonAsync(
            $"/api/tenants/{RootTenantId}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var secretBody = await secret.Content.ReadAsStringAsync();
        Assert.True(secret.StatusCode == HttpStatusCode.Created, secretBody);
        var material = JsonElement.Parse(secretBody).GetProperty("secretMaterial").GetString();
        Assert.NotNull(material);
        return (clientId, material, principalId);
    }

    private static async Task GrantTeamAdminAsync(
        HttpClient administrator,
        string teamId,
        string principalId
    )
    {
        using var grant = await administrator.PostAsJsonAsync(
            $"/api/teams/{teamId}/admins",
            new { principalId }
        );
        var body = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Created, body);
    }

    // Creates a client secret for a client of the control-plane tenant and returns the response.
    private static Task<HttpResponseMessage> PostClientSecretAsync(
        HttpClient client,
        string clientId
    ) =>
        client.PostAsJsonAsync(
            $"/api/tenants/{RootTenantId}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );

    private static async Task<HttpStatusCode> RequestClientCredentialsTokenAsync(
        HostApplicationFactory factory,
        string tenantId,
        string clientId,
        string clientSecret
    )
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                }
            ),
        };
        request.Headers.Host = $"{tenantId}.auth.test";
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

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

    #region Team Role Tests

    [Fact]
    public async Task TeamAdmin_CreatesATenantUnderItsTeamAndManagesItsClients()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        await CreateTenantAsync(teamAdmin, "acme", "acme-dev");

        using var created = await teamAdmin.PostAsJsonAsync(
            "/api/tenants/acme-dev/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                },
            }
        );
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);

        using var listed = await teamAdmin.GetAsync("/api/tenants/acme-dev/clients");
        var listedBody = await listed.Content.ReadAsStringAsync();
        Assert.True(listed.StatusCode == HttpStatusCode.OK, listedBody);

        await PatchAsync(
            teamAdmin,
            "/api/tenants/acme-dev/settings",
            new[]
            {
                new
                {
                    op = "add",
                    path = "/environment_type",
                    value = "development",
                },
            }
        );
    }

    [Fact]
    public async Task TeamAdmin_CreatesTwoClientsAndAResourceServerAndOwnsEach()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        var principalId = await SeedConsoleUserAsync(factory);
        using var grant = await administrator.PostAsJsonAsync(
            "/api/teams/acme/admins",
            new { principalId }
        );
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        using var teamAdmin = await factory.CreateManagementUserClientAsync(
            ConsoleClientId,
            ConsoleRedirectUri,
            UserName,
            Password,
            "openid read:clients"
        );
        await CreateTenantAsync(teamAdmin, "acme", "acme-dev");

        var clientIds = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            using var client = await teamAdmin.PostAsJsonAsync(
                "/api/tenants/acme-dev/clients",
                new
                {
                    isDisabled = false,
                    settings = new Dictionary<string, object>
                    {
                        ["token_endpoint_auth_method"] = OpenIdProtocol
                            .ClientAuthenticationMethods
                            .ClientSecretPost,
                    },
                }
            );
            var clientBody = await client.Content.ReadAsStringAsync();
            Assert.True(client.StatusCode == HttpStatusCode.Created, clientBody);
            clientIds.Add(JsonElement.Parse(clientBody).GetProperty("clientId").GetString()!);
        }

        using var resourceServer = await teamAdmin.PostAsJsonAsync(
            "/api/tenants/acme-dev/resource-servers",
            new
            {
                identifier = "https://api.acme-dev.test",
                name = "Acme API",
                isDisabled = false,
                settings = new { },
            }
        );
        var resourceServerBody = await resourceServer.Content.ReadAsStringAsync();
        Assert.True(resourceServer.StatusCode == HttpStatusCode.Created, resourceServerBody);
        var resourceServerId = JsonElement
            .Parse(resourceServerBody)
            .GetProperty("resourceServerId")
            .GetString();

        var ownersPaths = clientIds
            .Select(clientId => $"/api/tenants/acme-dev/clients/{clientId}/owners")
            .Append($"/api/tenants/acme-dev/resource-servers/{resourceServerId}/owners");
        foreach (var ownersPath in ownersPaths)
        {
            using var owners = await teamAdmin.GetAsync(ownersPath);
            var ownersBody = await owners.Content.ReadAsStringAsync();
            Assert.True(owners.StatusCode == HttpStatusCode.OK, ownersBody);
            Assert.Contains(
                JsonElement.Parse(ownersBody).GetProperty("items").EnumerateArray(),
                owner => owner.GetProperty("principalId").GetString() == principalId
            );
        }
    }

    [Fact]
    public async Task TeamAdmin_CannotCreateATenantUnderAnotherTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        using var response = await PostTenantAsync(teamAdmin, "globex", "globex-dev");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, body);
    }

    [Fact]
    public async Task TeamAdmin_CannotReadOrWriteThePlatformOrCreateATeam()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        using var read = await teamAdmin.GetAsync("/api/platform/settings");
        var readBody = await read.Content.ReadAsStringAsync();
        Assert.True(read.StatusCode == HttpStatusCode.Forbidden, readBody);

        using var write = await SendPatchAsync(
            teamAdmin,
            "/api/platform/settings",
            new[]
            {
                new
                {
                    op = "add",
                    path = "/environment_type",
                    value = "development",
                },
            }
        );
        var writeBody = await write.Content.ReadAsStringAsync();
        Assert.True(write.StatusCode == HttpStatusCode.Forbidden, writeBody);

        using var create = await teamAdmin.PostAsJsonAsync(
            "/api/teams",
            new
            {
                teamId = "globex",
                displayName = "globex",
                isDisabled = false,
                settings = new { },
            }
        );
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Forbidden, createBody);
    }

    [Fact]
    public async Task Owner_CannotCreateATenantOrGrantTeamAdmin()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        using var owner = await CreateTeamUserClientAsync(factory, administrator, "acme", "owners");

        using var tenant = await PostTenantAsync(owner, "acme", "acme-dev");
        var tenantBody = await tenant.Content.ReadAsStringAsync();
        Assert.True(tenant.StatusCode == HttpStatusCode.Forbidden, tenantBody);

        using var grant = await owner.PostAsJsonAsync(
            "/api/teams/acme/admins",
            new { principalId = "someone-else" }
        );
        var grantBody = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Forbidden, grantBody);
    }

    [Fact]
    public async Task PlatformAssignment_ListsTeamsAndTenantsAcrossTeams()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var principalId = await SeedConsoleUserAsync(factory);
        await GrantPlatformRoleAsync(factory, principalId, BuiltInRoles.TeamAdmin);
        using var user = await factory.CreateManagementUserClientAsync(
            ConsoleClientId,
            ConsoleRedirectUri,
            UserName,
            Password,
            "openid read:clients"
        );

        using var teams = await user.GetAsync("/api/teams");
        var teamsBody = await teams.Content.ReadAsStringAsync();
        Assert.True(teams.StatusCode == HttpStatusCode.OK, teamsBody);

        using var tenants = await user.GetAsync("/api/tenants");
        var tenantsBody = await tenants.Content.ReadAsStringAsync();
        Assert.True(tenants.StatusCode == HttpStatusCode.OK, tenantsBody);
    }

    [Fact]
    public async Task TeamAdmin_CannotReEnableATeamThePlatformDisabled()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );
        await SetTeamDisabledAsync(administrator, "acme", isDisabled: true);

        using var response = await SendPatchAsync(
            teamAdmin,
            "/api/teams/acme",
            new[]
            {
                new
                {
                    op = "replace",
                    path = "/isDisabled",
                    value = false,
                },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, body);
    }

    [Fact]
    public async Task DisableTeam_MakesItsTenantsAnswerNotFoundAtTheTokenEndpoint()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var (clientId, clientSecret) = await CreateSecretClientAsync(administrator, "acme-dev");

        Assert.Equal(
            HttpStatusCode.OK,
            await RequestClientCredentialsTokenAsync(factory, "acme-dev", clientId, clientSecret)
        );

        await SetTeamDisabledAsync(administrator, "acme", isDisabled: true);

        Assert.Equal(
            HttpStatusCode.NotFound,
            await RequestClientCredentialsTokenAsync(factory, "acme-dev", clientId, clientSecret)
        );

        await SetTeamDisabledAsync(administrator, "acme", isDisabled: false);

        Assert.Equal(
            HttpStatusCode.OK,
            await RequestClientCredentialsTokenAsync(factory, "acme-dev", clientId, clientSecret)
        );
    }

    #endregion

    #region Machine Client Tests

    [Fact]
    public async Task MachineClient_GrantedTeamAdmin_CreatesTenantsUnderItsTeamOnly()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        var (clientId, clientSecret, principalId) = await CreateMachineClientAsync(administrator);
        await GrantTeamAdminAsync(administrator, "acme", principalId);
        using var machine = await factory.CreateManagementClientAsync(clientId, clientSecret);

        await CreateTenantAsync(machine, "acme", "acme-dev");

        using var forbidden = await PostTenantAsync(machine, "globex", "globex-dev");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var owners = await machine.GetAsync("/api/tenants/acme-dev/owners");
        var ownersBody = await owners.Content.ReadAsStringAsync();
        Assert.True(owners.StatusCode == HttpStatusCode.OK, ownersBody);
        Assert.Contains(
            JsonElement.Parse(ownersBody).GetProperty("items").EnumerateArray(),
            owner => owner.GetProperty("principalId").GetString() == principalId
        );
    }

    [Fact]
    public async Task MachineClient_WithoutAnAssignment_CannotCreateATenant()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        var (clientId, clientSecret, _) = await CreateMachineClientAsync(administrator);
        using var machine = await factory.CreateManagementClientAsync(clientId, clientSecret);

        using var response = await PostTenantAsync(machine, "acme", "acme-dev");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MachineClient_WhenDisabled_LosesItsAuthority()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        var (clientId, clientSecret, principalId) = await CreateMachineClientAsync(administrator);
        await GrantTeamAdminAsync(administrator, "acme", principalId);
        using var machine = await factory.CreateManagementClientAsync(clientId, clientSecret);

        await PatchAsync(
            administrator,
            $"/api/tenants/{RootTenantId}/clients/{clientId}",
            new[]
            {
                new
                {
                    op = "replace",
                    path = "/isDisabled",
                    value = true,
                },
            }
        );

        using var response = await PostTenantAsync(machine, "acme", "acme-dev");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MachineClient_CredentialsAreManagedOnlyWithTheAuthorityItHolds()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        var (teamClientId, _, teamPrincipalId) = await CreateMachineClientAsync(administrator);
        await GrantTeamAdminAsync(administrator, "acme", teamPrincipalId);
        var (managerClientId, managerSecret, managerPrincipalId) = await CreateMachineClientAsync(
            administrator
        );
        await GrantTeamAdminAsync(administrator, "platform", managerPrincipalId);
        var (plainClientId, _, _) = await CreateMachineClientAsync(administrator);
        using var manager = await factory.CreateManagementClientAsync(
            managerClientId,
            managerSecret
        );

        // The manager administers the team of the control-plane tenant, so it manages the clients of that tenant that
        // hold no role.
        using var plain = await PostClientSecretAsync(manager, plainClientId);
        Assert.Equal(HttpStatusCode.Created, plain.StatusCode);

        // It cannot mint a secret for a client that administers a team it does not administer.
        using var refused = await PostClientSecretAsync(manager, teamClientId);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var granted = await PostClientSecretAsync(administrator, teamClientId);
        var grantedBody = await granted.Content.ReadAsStringAsync();
        Assert.True(granted.StatusCode == HttpStatusCode.Created, grantedBody);
    }

    #endregion

    #region Caller Tests

    [Fact]
    public async Task GetCaller_AsTheBootstrapAdministrator_HoldsPlatformAuthority()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.GetAsync("/api/me");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.True(JsonElement.Parse(body).GetProperty("isPlatformAdministrator").GetBoolean());
    }

    [Fact]
    public async Task GetCaller_AsATeamAdmin_ReportsItsTeamOnly()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        using var caller = await teamAdmin.GetAsync("/api/me");
        var callerBody = await caller.Content.ReadAsStringAsync();
        Assert.True(caller.StatusCode == HttpStatusCode.OK, callerBody);
        var callerJson = JsonElement.Parse(callerBody);
        Assert.False(callerJson.GetProperty("isPlatformAdministrator").GetBoolean());
        Assert.Contains(
            callerJson.GetProperty("assignments").EnumerateArray(),
            assignment =>
                assignment.GetProperty("roleName").GetString() == BuiltInRoles.TeamAdmin
                && assignment.GetProperty("resourceType").GetString() == "team"
                && assignment.GetProperty("resourceId").GetString() == "acme"
        );

        using var teams = await teamAdmin.GetAsync("/api/me/teams");
        var teamsBody = await teams.Content.ReadAsStringAsync();
        Assert.True(teams.StatusCode == HttpStatusCode.OK, teamsBody);
        Assert.Equal(
            ["acme"],
            JsonElement
                .Parse(teamsBody)
                .GetProperty("items")
                .EnumerateArray()
                .Select(team => team.GetProperty("teamId").GetString())
                .ToList()
        );

        using var allTeams = await teamAdmin.GetAsync("/api/teams");
        Assert.Equal(HttpStatusCode.Forbidden, allTeams.StatusCode);
    }

    [Fact]
    public async Task GetCallerTenants_AsATenantOwner_ListsTheTenantAndNoTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var principalId = await SeedConsoleUserAsync(factory);
        using var grant = await administrator.PostAsJsonAsync(
            "/api/tenants/acme-dev/owners",
            new { principalId }
        );
        var grantBody = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Created, grantBody);
        using var owner = await factory.CreateManagementUserClientAsync(
            ConsoleClientId,
            ConsoleRedirectUri,
            UserName,
            Password,
            "openid read:clients"
        );

        using var tenants = await owner.GetAsync("/api/me/tenants");
        var tenantsBody = await tenants.Content.ReadAsStringAsync();
        Assert.True(tenants.StatusCode == HttpStatusCode.OK, tenantsBody);
        Assert.Equal(
            ["acme-dev"],
            JsonElement
                .Parse(tenantsBody)
                .GetProperty("items")
                .EnumerateArray()
                .Select(tenant => tenant.GetProperty("tenantId").GetString())
                .ToList()
        );

        using var teams = await owner.GetAsync("/api/me/teams");
        var teamsBody = await teams.Content.ReadAsStringAsync();
        Assert.True(teams.StatusCode == HttpStatusCode.OK, teamsBody);
        Assert.Empty(JsonElement.Parse(teamsBody).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GetCaller_AsAMachineClient_ReportsItsPrincipal()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var (clientId, clientSecret, principalId) = await CreateMachineClientAsync(administrator);
        using var machine = await factory.CreateManagementClientAsync(clientId, clientSecret);

        using var response = await machine.GetAsync("/api/me");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal(principalId, JsonElement.Parse(body).GetProperty("principalId").GetString());
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

    [Fact]
    public async Task PatchTeamSettings_LeavesAnotherTeamsTenantsAlone()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(client, "globex", new { });
        await CreateTenantAsync(client, "globex", "globex-dev");
        await CreateTeamAsync(client, "hooli", new { });
        await CreateTenantAsync(client, "hooli", "hooli-dev");

        Assert.Contains("PS256", await GetIdTokenSigningAlgorithmsAsync(client, "globex-dev"));
        Assert.Contains("PS256", await GetIdTokenSigningAlgorithmsAsync(client, "hooli-dev"));

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

        Assert.Equal<string>(
            ["RS256"],
            await GetIdTokenSigningAlgorithmsAsync(client, "globex-dev")
        );

        var other = await GetIdTokenSigningAlgorithmsAsync(client, "hooli-dev");
        Assert.Contains("PS256", other);
        Assert.NotEqual<string>(["RS256"], other);
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

    [Fact]
    public async Task DisableTeam_KeepsItsTenantsManageable()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(client, "umbrella", new { });
        await CreateTenantAsync(client, "umbrella", "umbrella-dev");
        var (clientId, _) = await CreateSecretClientAsync(client, "umbrella-dev");

        await SetTeamDisabledAsync(client, "umbrella", isDisabled: true);

        using (var silenced = await GetDiscoveryAsync(client, "umbrella-dev"))
            Assert.Equal(HttpStatusCode.NotFound, silenced.StatusCode);

        using (
            var effective = await client.GetAsync("/api/tenants/umbrella-dev/effective-settings")
        )
            Assert.Equal(HttpStatusCode.OK, effective.StatusCode);

        using (var clients = await client.GetAsync("/api/tenants/umbrella-dev/clients"))
        {
            var clientsBody = await clients.Content.ReadAsStringAsync();
            Assert.True(clients.StatusCode == HttpStatusCode.OK, clientsBody);
            var ids = JsonElement
                .Parse(clientsBody)
                .GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("clientId").GetString())
                .ToList();
            Assert.Contains(clientId, ids);
        }

        // Revoking a session acts through the tenant's OpenID runtime, which a disabled team's tenant does not serve.
        // The runtime is materialized before the session is looked up, so an unknown session id answers 404 here.
        using (var revoked = await client.DeleteAsync("/api/tenants/umbrella-dev/sessions/unknown"))
            Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);

        // PatchAsync asserts 204 No Content.
        await PatchAsync(
            client,
            "/api/tenants/umbrella-dev/settings",
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

        await SetTeamDisabledAsync(client, "umbrella", isDisabled: false);

        using (var restored = await GetDiscoveryAsync(client, "umbrella-dev"))
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        // An unknown session is reported as revoked once the tenant is served again.
        using (
            var revokedAgain = await client.DeleteAsync(
                "/api/tenants/umbrella-dev/sessions/unknown"
            )
        )
            Assert.Equal(HttpStatusCode.NoContent, revokedAgain.StatusCode);
    }

    [Fact]
    public async Task DisableTeam_RefusesRuntimeWritesOnItsTenants()
    {
        using var factory = CreateFactory();
        using var client = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(client, "wonka", new { });
        await CreateTenantAsync(client, "wonka", "wonka-dev");

        string accountPath;
        using (
            var created = await client.PostAsJsonAsync(
                "/api/tenants/wonka-dev/local-accounts",
                new { userName = "wonka-user", password = "wonka-user-password-1" }
            )
        )
        {
            var createdBody = await created.Content.ReadAsStringAsync();
            Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
            var localAccountId = JsonElement
                .Parse(createdBody)
                .GetProperty("localAccountId")
                .GetString();
            accountPath = $"/api/tenants/wonka-dev/local-accounts/{localAccountId}";
        }

        await SetTeamDisabledAsync(client, "wonka", isDisabled: true);

        // A route that acts through the tenant's OpenID runtime materializes it before it writes, so a tenant that is
        // not served refuses the request and nothing is committed.
        using (var disabled = await client.PostAsync($"{accountPath}/disable", content: null))
            Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);

        using (
            var reset = await client.PostAsJsonAsync(
                $"{accountPath}/password",
                new { password = "wonka-user-password-2" }
            )
        )
            Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);

        // Deleting the account is refused too, and the account survives it.
        using (var deleted = await client.DeleteAsync(accountPath))
            Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);

        await SetTeamDisabledAsync(client, "wonka", isDisabled: false);

        using (var account = await client.GetAsync(accountPath))
        {
            var accountBody = await account.Content.ReadAsStringAsync();
            Assert.True(account.StatusCode == HttpStatusCode.OK, accountBody);
            Assert.True(
                JsonElement.Parse(accountBody).GetProperty("isEnabled").GetBoolean(),
                accountBody
            );
        }

        using (var disabledAgain = await client.PostAsync($"{accountPath}/disable", content: null))
        {
            var disabledBody = await disabledAgain.Content.ReadAsStringAsync();
            Assert.True(disabledAgain.StatusCode == HttpStatusCode.NoContent, disabledBody);
        }
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

    [Fact]
    public async Task TeamOwner_CannotDeleteTheTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator, "acme", new { });
        using var owner = await CreateTeamUserClientAsync(factory, administrator, "acme", "owners");

        using var forbidden = await owner.DeleteAsync("/api/teams/acme");
        var forbiddenBody = await forbidden.Content.ReadAsStringAsync();
        Assert.True(forbidden.StatusCode == HttpStatusCode.Forbidden, forbiddenBody);

        using var deleted = await administrator.DeleteAsync("/api/teams/acme");
        var deletedBody = await deleted.Content.ReadAsStringAsync();
        Assert.True(deleted.StatusCode == HttpStatusCode.NoContent, deletedBody);
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
