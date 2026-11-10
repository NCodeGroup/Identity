// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
/// how many it holds, it cannot be deleted while it holds any, and its usage lists each tenant's counts. A team can nest
/// beneath a parent team, which then supplies its settings, authority, limits, quota and usage.
/// </summary>
public class TeamManagementTests
{
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string RootTenantId = "root";
    private const string ConsoleClientId = "console";
    private const string ConsoleRedirectUri = "https://console.test/callback";
    private const string UserName = "team-user";
    private const string Password = "team-user-password";
    private const string DiscoveryPath = "/.well-known/openid-configuration";

    #region Scaffolding

    private static BootstrapAdminApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByHost,
                ["OpenId:TenantResolution:Root:Issuer"] = RootIssuer,
                ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = IssuerTemplate,
            },
            RootTenantId
        );

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

    private static async Task<HttpResponseMessage> PostChildTeamAsync(
        HttpClient client,
        string parentTeamId,
        string teamId,
        object settings
    ) =>
        await client.PostAsJsonAsync(
            $"/api/teams/{parentTeamId}/teams",
            new
            {
                teamId,
                displayName = teamId,
                isDisabled = false,
                settings,
            }
        );

    private static async Task CreateChildTeamAsync(
        HttpClient client,
        string parentTeamId,
        string teamId,
        object settings
    )
    {
        using var response = await PostChildTeamAsync(client, parentTeamId, teamId, settings);
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
            "openid read:clients",
            CancellationToken.None
        );
    }

    // Seeds the same end-user and console client, grants the user the role by posting its principal to the full grant
    // path (such as /api/tenants/acme-dev/admins), and returns a client that calls the management API with the user's
    // access token.
    private static async Task<HttpClient> CreateGrantedUserClientAsync(
        HostApplicationFactory factory,
        HttpClient administrator,
        string grantPath
    )
    {
        var principalId = await SeedConsoleUserAsync(factory);

        using var grant = await administrator.PostAsJsonAsync(grantPath, new { principalId });
        var body = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Created, body);

        return await factory.CreateManagementUserClientAsync(
            ConsoleClientId,
            ConsoleRedirectUri,
            UserName,
            Password,
            "openid read:clients",
            CancellationToken.None
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
        var consoleSettings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { ConsoleRedirectUri },
                ["token_endpoint_auth_method"] = OpenIdProtocol.ClientAuthenticationMethods.None,
            }
        );
        var consoleClient = new PersistedClientBuilder
        {
            TenantId = RootTenantId,
            ClientId = ConsoleClientId,
            Settings = consoleSettings,
        }.Build();
        await storeManager.GetStore<IClientStore>().AddAsync(consoleClient, CancellationToken.None);
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

    private static async Task AssertGetStatusAsync(
        HttpClient client,
        string path,
        HttpStatusCode expected
    )
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{path}: {body}");
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

    // Creates a machine client of the platform, or of the node that ownerPath names, that authenticates with HTTP Basic
    // and returns its credentials and the principal that its tokens resolve to.
    private static async Task<(
        string ClientId,
        string ClientSecret,
        string PrincipalId
    )> CreateMachineClientAsync(HttpClient caller, string ownerPath = "/api/platform/clients")
    {
        using var created = await caller.PostAsJsonAsync(
            ownerPath,
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

        using var secret = await caller.PostAsJsonAsync(
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

    // Creates team acme with tenants acme-dev and acme-prod and fills acme-dev with a development environment, a
    // resource server with a scope, a client with a secret and a grant of the client to the resource server; returns
    // the client's id and the resource server's id.
    private static async Task<(
        string ClientId,
        string ResourceServerId
    )> ArrangePromotionSourceAsync(HttpClient administrator)
    {
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        await CreateTenantAsync(administrator, "acme", "acme-prod");
        await PatchAsync(
            administrator,
            "/api/tenants/acme-dev/settings",
            new[]
            {
                new
                {
                    op = "add",
                    path = $"/{OpenIdSettingNames.RequireConsent}",
                    value = (object)true,
                },
                new
                {
                    op = "add",
                    path = $"/{OpenIdSettingNames.EnvironmentType}",
                    value = (object)"development",
                },
                new
                {
                    op = "add",
                    path = $"/{OpenIdSettingNames.EnvironmentName}",
                    value = (object)"sandbox",
                },
            }
        );

        using var resourceServer = await administrator.PostAsJsonAsync(
            "/api/tenants/acme-dev/resource-servers",
            new
            {
                identifier = "https://api.acme.test",
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
        Assert.NotNull(resourceServerId);

        using var scope = await administrator.PostAsJsonAsync(
            $"/api/tenants/acme-dev/resource-servers/{resourceServerId}/scopes",
            new { value = "read:things", description = (string?)null }
        );
        var scopeBody = await scope.Content.ReadAsStringAsync();
        Assert.True(scope.StatusCode == HttpStatusCode.Created, scopeBody);

        using var created = await administrator.PostAsJsonAsync(
            "/api/tenants/acme-dev/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    [OpenIdSettingNames.TokenEndpointAuthMethod] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                    [OpenIdSettingNames.ClientName] = "Acme Web",
                    [OpenIdSettingNames.RedirectUris] = new[] { "https://dev.acme.test/cb" },
                },
            }
        );
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
        var clientId = JsonElement.Parse(createdBody).GetProperty("clientId").GetString();
        Assert.NotNull(clientId);

        using var secret = await administrator.PostAsJsonAsync(
            $"/api/tenants/acme-dev/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var secretBody = await secret.Content.ReadAsStringAsync();
        Assert.True(secret.StatusCode == HttpStatusCode.Created, secretBody);

        using var grant = await administrator.PostAsJsonAsync(
            $"/api/tenants/acme-dev/clients/{clientId}/grants",
            new { resourceServerId, scopes = new[] { "read:things" } }
        );
        var grantBody = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Created, grantBody);

        return (clientId, resourceServerId);
    }

    private static Task<HttpResponseMessage> PostPromotionAsync(
        HttpClient client,
        string teamId,
        string route,
        string sourceTenantId,
        string targetTenantId,
        object? environmentSettings = null
    ) =>
        client.PostAsJsonAsync(
            $"/api/teams/{teamId}/promotions{route}",
            new
            {
                sourceTenantId,
                targetTenantId,
                environmentSettings = environmentSettings ?? Array.Empty<object>(),
            }
        );

    private static async Task<JsonElement> ReadPromotionAsync(
        HttpResponseMessage response,
        HttpStatusCode expected
    )
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, body);
        return JsonElement.Parse(body);
    }

    private static bool HasChange(
        JsonElement report,
        string resourceType,
        string resourceId,
        string action
    ) =>
        report
            .GetProperty("changes")
            .EnumerateArray()
            .Any(change =>
                change.GetProperty("resourceType").GetString() == resourceType
                && change.GetProperty("resourceId").GetString() == resourceId
                && change.GetProperty("action").GetString() == action
            );

    private static bool HasNote(
        JsonElement report,
        string resourceId,
        string code,
        string? settingName = null
    ) =>
        report
            .GetProperty("notes")
            .EnumerateArray()
            .Any(note =>
                note.GetProperty("resourceId").GetString() == resourceId
                && note.GetProperty("code").GetString() == code
                && (
                    settingName is null
                    || (
                        note.TryGetProperty("settingName", out var name)
                        && name.GetString() == settingName
                    )
                )
            );

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {body}");
        return JsonElement.Parse(body);
    }

    #endregion

    #region CreateTeam Tests

    [Fact]
    public async Task CreateTeam_ThenCreateItsTenant_ServesTheTenantUnderTheTeamsSettings()
    {
        using var factory = CreateFactory();
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);

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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
            "openid read:clients",
            CancellationToken.None
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var principalId = await SeedConsoleUserAsync(factory);
        await GrantPlatformRoleAsync(factory, principalId, BuiltInRoles.TeamAdmin);
        using var user = await factory.CreateManagementUserClientAsync(
            ConsoleClientId,
            ConsoleRedirectUri,
            UserName,
            Password,
            "openid read:clients",
            CancellationToken.None
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var (clientId, clientSecret) = await CreateSecretClientAsync(administrator, "acme-dev");

        var tokenStatusCode = await RequestClientCredentialsTokenAsync(
            factory,
            "acme-dev",
            clientId,
            clientSecret
        );
        Assert.Equal(HttpStatusCode.OK, tokenStatusCode);

        await SetTeamDisabledAsync(administrator, "acme", isDisabled: true);

        var disabledStatusCode = await RequestClientCredentialsTokenAsync(
            factory,
            "acme-dev",
            clientId,
            clientSecret
        );
        Assert.Equal(HttpStatusCode.NotFound, disabledStatusCode);

        await SetTeamDisabledAsync(administrator, "acme", isDisabled: false);

        var reenabledStatusCode = await RequestClientCredentialsTokenAsync(
            factory,
            "acme-dev",
            clientId,
            clientSecret
        );
        Assert.Equal(HttpStatusCode.OK, reenabledStatusCode);
    }

    #endregion

    #region Machine Client Tests

    [Fact]
    public async Task MachineClient_GrantedTeamAdmin_CreatesTenantsUnderItsTeamOnly()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        var (clientId, clientSecret, principalId) = await CreateMachineClientAsync(administrator);
        await GrantTeamAdminAsync(administrator, "acme", principalId);
        using var machine = await factory.CreateManagementClientAsync(
            clientId,
            clientSecret,
            CancellationToken.None
        );

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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        var (clientId, clientSecret, _) = await CreateMachineClientAsync(administrator);
        using var machine = await factory.CreateManagementClientAsync(
            clientId,
            clientSecret,
            CancellationToken.None
        );

        using var response = await PostTenantAsync(machine, "acme", "acme-dev");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MachineClient_WhenDisabled_LosesItsAuthority()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        var (clientId, clientSecret, principalId) = await CreateMachineClientAsync(administrator);
        await GrantTeamAdminAsync(administrator, "acme", principalId);
        using var machine = await factory.CreateManagementClientAsync(
            clientId,
            clientSecret,
            CancellationToken.None
        );

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
    public async Task PostClientSecret_ForATeamsMachineClient_ByAnotherTeamsMachineClient_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        var (acmeClientId, _, _) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/acme/clients"
        );
        var (managerClientId, managerSecret, managerPrincipalId) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/globex/clients"
        );
        await GrantTeamAdminAsync(administrator, "globex", managerPrincipalId);
        using var manager = await factory.CreateManagementClientAsync(
            managerClientId,
            managerSecret,
            CancellationToken.None
        );

        using var response = await PostClientSecretAsync(manager, acmeClientId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostClientSecret_ByTheControlPlaneTenantAdministrator_ForAMachineClientThatHoldsNoRole_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        var (teamClientId, _, _) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/acme/clients"
        );
        var (platformClientId, _, _) = await CreateMachineClientAsync(administrator);
        using var tenantAdmin = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            $"/api/tenants/{RootTenantId}/admins"
        );

        using var teamResponse = await PostClientSecretAsync(tenantAdmin, teamClientId);
        using var platformResponse = await PostClientSecretAsync(tenantAdmin, platformClientId);

        Assert.Equal(HttpStatusCode.Forbidden, teamResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, platformResponse.StatusCode);
    }

    [Fact]
    public async Task PostClientSecret_ByTheTeamsAdministrator_ForItsMachineClientThatHoldsTheTeamsRole_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        var (teamClientId, _, teamPrincipalId) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/acme/clients"
        );
        await GrantTeamAdminAsync(administrator, "acme", teamPrincipalId);
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        using var response = await PostClientSecretAsync(teamAdmin, teamClientId);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task PostClientSecret_WhenTheCallerIsAGlobalAdmin_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        var (teamClientId, _, teamPrincipalId) = await CreateMachineClientAsync(administrator);
        await GrantTeamAdminAsync(administrator, "acme", teamPrincipalId);

        using var response = await PostClientSecretAsync(administrator, teamClientId);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task GrantTeamAdmin_ToAnOrdinaryClient_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        using var created = await administrator.PostAsJsonAsync(
            $"/api/tenants/{RootTenantId}/clients",
            new { isDisabled = false, settings = new { } }
        );
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
        var principalId = JsonElement.Parse(createdBody).GetProperty("principalId").GetString();
        Assert.NotNull(principalId);

        using var response = await administrator.PostAsJsonAsync(
            "/api/teams/acme/admins",
            new { principalId }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    [Fact]
    public async Task GrantTeamAdmin_ToAnotherTeamsMachineClient_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        var (_, _, globexPrincipalId) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/globex/clients"
        );

        using var response = await administrator.PostAsJsonAsync(
            "/api/teams/acme/admins",
            new { principalId = globexPrincipalId }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    [Fact]
    public async Task GrantTenantOwner_ToTheTeamsMachineClient_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var (_, _, acmePrincipalId) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/acme/clients"
        );

        using var response = await administrator.PostAsJsonAsync(
            "/api/tenants/acme-dev/owners",
            new { principalId = acmePrincipalId }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task GrantTeamAdmin_ToThePlatformsMachineClient_ReturnsCreated()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        var (_, _, platformPrincipalId) = await CreateMachineClientAsync(administrator);

        using var response = await administrator.PostAsJsonAsync(
            "/api/teams/acme/admins",
            new { principalId = platformPrincipalId }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task TeamAdmin_CreatesAMachineClientThatAdministersItsTeamOnly()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        using var user = await CreateTeamUserClientAsync(factory, administrator, "acme", "admins");

        var (clientId, clientSecret, principalId) = await CreateMachineClientAsync(
            user,
            "/api/teams/acme/clients"
        );

        using var read = await user.GetAsync($"/api/tenants/{RootTenantId}/clients/{clientId}");
        var readBody = await read.Content.ReadAsStringAsync();
        Assert.True(read.StatusCode == HttpStatusCode.OK, readBody);
        var readJson = JsonElement.Parse(readBody);
        Assert.Equal("team", readJson.GetProperty("owningNodeType").GetString());
        Assert.Equal("acme", readJson.GetProperty("owningNodeId").GetString());

        await GrantTeamAdminAsync(user, "acme", principalId);
        using var machine = await factory.CreateManagementClientAsync(
            clientId,
            clientSecret,
            CancellationToken.None
        );

        await CreateTenantAsync(machine, "acme", "acme-dev");

        using var forbidden = await PostTenantAsync(machine, "globex", "globex-dev");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ListTeamClients_ListsOnlyTheTeamsMachineClients()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        var (acmeClientId, _, _) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/acme/clients"
        );
        var (globexClientId, _, _) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/globex/clients"
        );

        using var response = await administrator.GetAsync("/api/teams/acme/clients");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var clientIds = JsonElement
            .Parse(body)
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("clientId").GetString())
            .ToList();
        Assert.Contains(acmeClientId, clientIds);
        Assert.DoesNotContain(globexClientId, clientIds);
    }

    [Fact]
    public async Task CreatePlatformClient_IsOwnedByThePlatformAndListedThere()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var created = await administrator.PostAsJsonAsync(
            "/api/platform/clients",
            new { isDisabled = false, settings = new { } }
        );

        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
        var createdJson = JsonElement.Parse(createdBody);
        Assert.Equal("platform", createdJson.GetProperty("owningNodeType").GetString());
        Assert.Equal("platform", createdJson.GetProperty("owningNodeId").GetString());
        var clientId = createdJson.GetProperty("clientId").GetString();

        using var listed = await administrator.GetAsync("/api/platform/clients");

        var listedBody = await listed.Content.ReadAsStringAsync();
        Assert.True(listed.StatusCode == HttpStatusCode.OK, listedBody);
        Assert.Contains(
            JsonElement.Parse(listedBody).GetProperty("items").EnumerateArray(),
            item => item.GetProperty("clientId").GetString() == clientId
        );
    }

    [Fact]
    public async Task TeamAdmin_GetsForbiddenForAnotherTeamsAndThePlatformsMachineClients()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        foreach (var path in new[] { "/api/teams/globex/clients", "/api/platform/clients" })
        {
            using var created = await teamAdmin.PostAsJsonAsync(
                path,
                new { isDisabled = false, settings = new { } }
            );
            var createdBody = await created.Content.ReadAsStringAsync();
            Assert.True(created.StatusCode == HttpStatusCode.Forbidden, $"{path}: {createdBody}");

            await AssertGetStatusAsync(teamAdmin, path, HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task CreateTeamClient_WithAnInteractiveGrantType_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });

        using var response = await administrator.PostAsJsonAsync(
            "/api/teams/acme/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    [OpenIdSettingNames.GrantTypesSupported] = new[] { "authorization_code" },
                },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
    }

    [Fact]
    public async Task CreateTeamClient_ForATeamThatDoesNotExist_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsJsonAsync(
            "/api/teams/missing/clients",
            new { isDisabled = false, settings = new { } }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NotFound, body);
    }

    [Fact]
    public async Task PatchMachineClientSettings_WithAnotherGrantType_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        var (clientId, _, _) = await CreateMachineClientAsync(
            administrator,
            "/api/teams/acme/clients"
        );

        using var response = await SendPatchAsync(
            administrator,
            $"/api/tenants/{RootTenantId}/clients/{clientId}/settings",
            new[]
            {
                new
                {
                    op = "replace",
                    path = "/grant_types_supported",
                    value = new[] { "authorization_code", "client_credentials" },
                },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
    }

    [Fact]
    public async Task DeleteTeam_WhileItOwnsAMachineClient_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateMachineClientAsync(administrator, "/api/teams/acme/clients");

        using var response = await administrator.DeleteAsync("/api/teams/acme");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    #endregion

    #region Caller Tests

    [Fact]
    public async Task GetCaller_AsTheBootstrapAdministrator_HoldsPlatformAuthority()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.GetAsync("/api/me");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.True(JsonElement.Parse(body).GetProperty("isPlatformAdministrator").GetBoolean());
    }

    [Fact]
    public async Task GetCaller_WhenTheCallerIsATeamAdmin_ReportsItsAssignment()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
    }

    [Fact]
    public async Task GetCallerTeams_WhenTheCallerIsATeamAdmin_ListsItsTeamOnly()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
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
    }

    [Fact]
    public async Task ListTeams_WhenTheCallerIsATeamAdmin_IsForbidden()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        using var allTeams = await teamAdmin.GetAsync("/api/teams");

        Assert.Equal(HttpStatusCode.Forbidden, allTeams.StatusCode);
    }

    [Fact]
    public async Task GetCallerTenants_AsATenantOwner_ListsTheTenantAndNoTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
            "openid read:clients",
            CancellationToken.None
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var (clientId, clientSecret, principalId) = await CreateMachineClientAsync(administrator);
        using var machine = await factory.CreateManagementClientAsync(
            clientId,
            clientSecret,
            CancellationToken.None
        );

        using var response = await machine.GetAsync("/api/me");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal(principalId, JsonElement.Parse(body).GetProperty("principalId").GetString());
    }

    #endregion

    #region Tenant Role Tests

    [Fact]
    public async Task TenantAdmin_CreatesAClientInItsTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        using var tenantAdmin = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/admins"
        );

        using var created = await tenantAdmin.PostAsJsonAsync(
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

        var body = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task TenantAdmin_CannotReachItsTeamOrCreateATenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        using var tenantAdmin = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/admins"
        );

        using var team = await tenantAdmin.GetAsync("/api/teams/acme");
        var teamBody = await team.Content.ReadAsStringAsync();
        Assert.True(team.StatusCode == HttpStatusCode.Forbidden, teamBody);

        using var tenant = await PostTenantAsync(tenantAdmin, "acme", "acme-test");
        var tenantBody = await tenant.Content.ReadAsStringAsync();
        Assert.True(tenant.StatusCode == HttpStatusCode.Forbidden, tenantBody);
    }

    [Fact]
    public async Task TenantAdminOfTheRootTenant_CannotReachThePlatformOrATeam()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        using var user = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            $"/api/tenants/{RootTenantId}/admins"
        );

        using var clients = await user.GetAsync($"/api/tenants/{RootTenantId}/clients");
        var clientsBody = await clients.Content.ReadAsStringAsync();
        Assert.True(clients.StatusCode == HttpStatusCode.OK, clientsBody);

        using var platform = await user.GetAsync("/api/platform/settings");
        var platformBody = await platform.Content.ReadAsStringAsync();
        Assert.True(platform.StatusCode == HttpStatusCode.Forbidden, platformBody);

        using var teams = await user.GetAsync("/api/teams");
        var teamsBody = await teams.Content.ReadAsStringAsync();
        Assert.True(teams.StatusCode == HttpStatusCode.Forbidden, teamsBody);

        using var intruder = await PostTenantAsync(user, "platform", "intruder");
        var intruderBody = await intruder.Content.ReadAsStringAsync();
        Assert.True(intruder.StatusCode == HttpStatusCode.Forbidden, intruderBody);
    }

    [Fact]
    public async Task TenantAdminOfTheRootTenant_CannotTakeOverTheBootstrapClient()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        using var user = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            $"/api/tenants/{RootTenantId}/admins"
        );

        foreach (var caller in new[] { user, administrator })
        {
            using var secret = await caller.PostAsJsonAsync(
                $"/api/tenants/{RootTenantId}/clients/{BootstrapAdminApplicationFactory.ClientId}/secrets",
                new
                {
                    secretType = "symmetric",
                    keySizeBits = 256,
                    expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
                }
            );
            var secretBody = await secret.Content.ReadAsStringAsync();
            Assert.True(secret.StatusCode == HttpStatusCode.Conflict, secretBody);

            using var settings = await SendPatchAsync(
                caller,
                $"/api/tenants/{RootTenantId}/clients/{BootstrapAdminApplicationFactory.ClientId}/settings",
                new[]
                {
                    new
                    {
                        op = "add",
                        path = "/redirect_uris",
                        value = new[] { "https://attacker.test/callback" },
                    },
                }
            );
            var settingsBody = await settings.Content.ReadAsStringAsync();
            Assert.True(settings.StatusCode == HttpStatusCode.Conflict, settingsBody);
        }
    }

    #endregion

    #region Authorization Order Tests

    [Fact]
    public async Task TeamAdmin_GetsForbiddenForAnUnknownTeamAndAnotherTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        await AssertGetStatusAsync(teamAdmin, "/api/teams/globex", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(teamAdmin, "/api/teams/nope", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(teamAdmin, "/api/teams/nope/settings", HttpStatusCode.Forbidden);

        using var patch = await SendPatchAsync(
            teamAdmin,
            "/api/teams/nope/settings",
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
        var patchBody = await patch.Content.ReadAsStringAsync();
        Assert.True(patch.StatusCode == HttpStatusCode.Forbidden, patchBody);

        using var create = await PostTenantAsync(teamAdmin, "nope", "nope-dev");
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Forbidden, createBody);

        await AssertGetStatusAsync(teamAdmin, "/api/teams/nope/admins", HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TeamAdmin_GetsForbiddenForAnUnknownTenantAndAnotherTeamsTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateTenantAsync(administrator, "globex", "globex-dev");
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        await AssertGetStatusAsync(teamAdmin, "/api/tenants/globex-dev", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(teamAdmin, "/api/tenants/nope", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(
            teamAdmin,
            "/api/tenants/nope/settings",
            HttpStatusCode.Forbidden
        );
        await AssertGetStatusAsync(
            teamAdmin,
            "/api/tenants/nope/secrets",
            HttpStatusCode.Forbidden
        );
        await AssertGetStatusAsync(
            teamAdmin,
            "/api/tenants/nope/encryption-keys",
            HttpStatusCode.Forbidden
        );
        await AssertGetStatusAsync(teamAdmin, "/api/tenants/nope/admins", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(
            teamAdmin,
            "/api/tenants/globex-dev/owners",
            HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task TeamAdmin_GetsForbiddenForWritesInAnotherTeamsTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateTenantAsync(administrator, "globex", "globex-dev");
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        using var patchClient = await SendPatchAsync(
            teamAdmin,
            "/api/tenants/globex-dev/clients/nope",
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
        var patchClientBody = await patchClient.Content.ReadAsStringAsync();
        Assert.True(patchClient.StatusCode == HttpStatusCode.Forbidden, patchClientBody);

        using var deleteClient = await teamAdmin.DeleteAsync(
            "/api/tenants/globex-dev/clients/nope"
        );
        var deleteClientBody = await deleteClient.Content.ReadAsStringAsync();
        Assert.True(deleteClient.StatusCode == HttpStatusCode.Forbidden, deleteClientBody);

        using var patchSettings = await SendPatchAsync(
            teamAdmin,
            "/api/tenants/globex-dev/clients/nope/settings",
            new[]
            {
                new
                {
                    op = "replace",
                    path = "/client_name",
                    value = "x",
                },
            }
        );
        var patchSettingsBody = await patchSettings.Content.ReadAsStringAsync();
        Assert.True(patchSettings.StatusCode == HttpStatusCode.Forbidden, patchSettingsBody);

        using var postSecret = await teamAdmin.PostAsJsonAsync(
            "/api/tenants/globex-dev/clients/nope/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var postSecretBody = await postSecret.Content.ReadAsStringAsync();
        Assert.True(postSecret.StatusCode == HttpStatusCode.Forbidden, postSecretBody);

        using var deleteSecret = await teamAdmin.DeleteAsync(
            "/api/tenants/globex-dev/clients/nope/secrets/nope"
        );
        var deleteSecretBody = await deleteSecret.Content.ReadAsStringAsync();
        Assert.True(deleteSecret.StatusCode == HttpStatusCode.Forbidden, deleteSecretBody);

        using var patchResourceServer = await SendPatchAsync(
            teamAdmin,
            "/api/tenants/globex-dev/resource-servers/nope",
            new[]
            {
                new
                {
                    op = "replace",
                    path = "/name",
                    value = "x",
                },
            }
        );
        var patchResourceServerBody = await patchResourceServer.Content.ReadAsStringAsync();
        Assert.True(
            patchResourceServer.StatusCode == HttpStatusCode.Forbidden,
            patchResourceServerBody
        );

        using var deleteScope = await teamAdmin.DeleteAsync(
            "/api/tenants/globex-dev/resource-servers/nope/scopes/read"
        );
        var deleteScopeBody = await deleteScope.Content.ReadAsStringAsync();
        Assert.True(deleteScope.StatusCode == HttpStatusCode.Forbidden, deleteScopeBody);

        using var deleteGrant = await teamAdmin.DeleteAsync(
            "/api/tenants/globex-dev/clients/nope/grants/nope"
        );
        var deleteGrantBody = await deleteGrant.Content.ReadAsStringAsync();
        Assert.True(deleteGrant.StatusCode == HttpStatusCode.Forbidden, deleteGrantBody);

        using var deleteAccount = await teamAdmin.DeleteAsync(
            "/api/tenants/globex-dev/local-accounts/nope"
        );
        var deleteAccountBody = await deleteAccount.Content.ReadAsStringAsync();
        Assert.True(deleteAccount.StatusCode == HttpStatusCode.Forbidden, deleteAccountBody);
    }

    [Fact]
    public async Task ClientOwner_ManagesItsClientsSecretsButCreatesNone()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var (clientId, _) = await CreateSecretClientAsync(administrator, "acme-dev");
        using var owner = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            $"/api/tenants/acme-dev/clients/{clientId}/owners"
        );

        var secrets = await GetJsonAsync(
            owner,
            $"/api/tenants/acme-dev/clients/{clientId}/secrets"
        );
        var secretId = secrets.GetProperty("secrets")[0].GetProperty("secretId").GetString();
        Assert.NotNull(secretId);

        using var create = await owner.PostAsJsonAsync(
            $"/api/tenants/acme-dev/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Forbidden, createBody);

        using var delete = await owner.DeleteAsync(
            $"/api/tenants/acme-dev/clients/{clientId}/secrets/{secretId}"
        );
        var deleteBody = await delete.Content.ReadAsStringAsync();
        Assert.True(delete.StatusCode == HttpStatusCode.NoContent, deleteBody);

        using var otherClient = await owner.DeleteAsync(
            $"/api/tenants/acme-dev/clients/nope/secrets/{secretId}"
        );
        var otherClientBody = await otherClient.Content.ReadAsStringAsync();
        Assert.True(otherClient.StatusCode == HttpStatusCode.Forbidden, otherClientBody);
    }

    [Fact]
    public async Task TenantAdmin_GetsForbiddenForAnUnknownTenantAndASiblingTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        await CreateTenantAsync(administrator, "acme", "acme-prod");
        using var tenantAdmin = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/admins"
        );

        await AssertGetStatusAsync(tenantAdmin, "/api/tenants/acme-prod", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(tenantAdmin, "/api/tenants/nope", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(
            tenantAdmin,
            "/api/tenants/nope/admins",
            HttpStatusCode.Forbidden
        );

        using var grant = await tenantAdmin.PostAsJsonAsync(
            "/api/tenants/nope/admins",
            new { principalId = "pid-x" }
        );
        var grantBody = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Forbidden, grantBody);
    }

    [Fact]
    public async Task Administrator_GetsNotFoundForAnUnknownTeamAndTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        await AssertGetStatusAsync(administrator, "/api/teams/nope", HttpStatusCode.NotFound);
        await AssertGetStatusAsync(administrator, "/api/tenants/nope", HttpStatusCode.NotFound);
        await AssertGetStatusAsync(
            administrator,
            "/api/tenants/nope/admins",
            HttpStatusCode.NotFound
        );
    }

    #endregion

    #region Limit Tests

    [Fact]
    public async Task TeamAdmin_CannotChangeItsTeamsLimits()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { max_tenants = 2 });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        using var replaced = await SendPatchAsync(
            teamAdmin,
            "/api/teams/acme/settings",
            new[]
            {
                new
                {
                    op = "replace",
                    path = $"/{OpenIdSettingNames.MaxTenants}",
                    value = 5,
                },
            }
        );
        var replacedBody = await replaced.Content.ReadAsStringAsync();
        Assert.True(replaced.StatusCode == HttpStatusCode.Forbidden, replacedBody);
        Assert.Contains(OpenIdSettingNames.MaxTenants, replacedBody);

        using var removed = await SendPatchAsync(
            teamAdmin,
            "/api/teams/acme/settings",
            new[] { new { op = "remove", path = $"/{OpenIdSettingNames.MaxTenants}" } }
        );
        var removedBody = await removed.Content.ReadAsStringAsync();
        Assert.True(removed.StatusCode == HttpStatusCode.Forbidden, removedBody);

        await PatchAsync(
            teamAdmin,
            "/api/teams/acme/settings",
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
    }

    [Fact]
    public async Task PlatformAdministrator_ChangesATeamsLimits()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { max_tenants = 2 });

        await PatchAsync(
            administrator,
            "/api/teams/acme/settings",
            new[]
            {
                new
                {
                    op = "replace",
                    path = $"/{OpenIdSettingNames.MaxTenants}",
                    value = 5,
                },
            }
        );
    }

    [Fact]
    public async Task TeamAdmin_SetsALimitOnItsTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );
        await CreateTenantAsync(teamAdmin, "acme", "acme-dev");

        await PatchAsync(
            teamAdmin,
            "/api/tenants/acme-dev/settings",
            new[]
            {
                new
                {
                    op = "add",
                    path = $"/{OpenIdSettingNames.RateLimitTenantPerMinute}",
                    value = 100,
                },
            }
        );
    }

    [Fact]
    public async Task TenantOwner_CannotChangeItsTenantsLimits()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        using var owner = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/owners"
        );

        using var response = await SendPatchAsync(
            owner,
            "/api/tenants/acme-dev/settings",
            new[]
            {
                new
                {
                    op = "add",
                    path = $"/{OpenIdSettingNames.RateLimitTenantPerMinute}",
                    value = 100,
                },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, body);
        Assert.Contains(
            OpenIdSettingNames.RateLimitTenantPerMinute,
            body,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region GetSettingLimits Tests

    [Fact]
    public async Task GetSettingLimits_AsATeamAdmin_ReportsItsTeamsLimitsItCannotChange()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { max_tenants = 2 });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        var limits = await GetJsonAsync(teamAdmin, "/api/teams/acme/settings/limits");

        Assert.Contains(OpenIdSettingNames.MaxTenants, GetLimitNames(limits));
        Assert.False(limits.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task GetSettingLimits_AsATeamAdmin_ReportsItsTenantsLimitsItCanChange()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );
        await CreateTenantAsync(teamAdmin, "acme", "acme-dev");

        var limits = await GetJsonAsync(teamAdmin, "/api/tenants/acme-dev/settings/limits");

        Assert.Contains(OpenIdSettingNames.RateLimitTenantPerMinute, GetLimitNames(limits));
        Assert.True(limits.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task GetSettingLimits_AsATenantOwner_ReportsItsTenantsLimitsItCannotChange()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        using var owner = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/owners"
        );

        var limits = await GetJsonAsync(owner, "/api/tenants/acme-dev/settings/limits");

        Assert.Contains(OpenIdSettingNames.RateLimitTenantPerMinute, GetLimitNames(limits));
        Assert.False(limits.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task GetSettingLimits_AsThePlatformAdministrator_ReportsATeamsLimitsItCanChange()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { max_tenants = 2 });

        var limits = await GetJsonAsync(administrator, "/api/teams/acme/settings/limits");

        Assert.Contains(OpenIdSettingNames.MaxTenants, GetLimitNames(limits));
        Assert.True(limits.GetProperty("canChange").GetBoolean());
    }

    private static string[] GetLimitNames(JsonElement limits) =>
        [
            .. limits
                .GetProperty("names")
                .EnumerateArray()
                .Select(name =>
                    name.GetString() ?? throw new InvalidOperationException("A limit name is null.")
                ),
        ];

    #endregion

    #region ListRoleAssignments Tests

    [Fact]
    public async Task ListRoleAssignments_AsATenantOwner_ReportsItMayChangeTheOwners()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        using var owner = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/owners"
        );

        var owners = await GetJsonAsync(owner, "/api/tenants/acme-dev/owners");

        Assert.True(owners.GetProperty("canChange").GetBoolean());
        Assert.NotEmpty(owners.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ListRoleAssignments_AsATenantOwner_ReportsItMayNotChangeTheAdministrators()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        using var owner = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/owners"
        );

        var admins = await GetJsonAsync(owner, "/api/tenants/acme-dev/admins");

        Assert.False(admins.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task ListRoleAssignments_AsATeamAdmin_ReportsItMayChangeItsTenantsAdministrators()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );
        await CreateTenantAsync(teamAdmin, "acme", "acme-dev");

        var admins = await GetJsonAsync(teamAdmin, "/api/tenants/acme-dev/admins");

        Assert.True(admins.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task ListRoleAssignments_AsATeamAdmin_ReportsItMayChangeItsTeamsAdministrators()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        using var teamAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        var admins = await GetJsonAsync(teamAdmin, "/api/teams/acme/admins");

        Assert.True(admins.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task ListRoleAssignments_AsATeamOwner_ReportsItMayChangeItsTeamsOwners()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        using var owner = await CreateTeamUserClientAsync(factory, administrator, "acme", "owners");

        var owners = await GetJsonAsync(owner, "/api/teams/acme/owners");

        Assert.True(owners.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task ListRoleAssignments_AsAnAdministrator_ReportsItMayChangeAClientsOwners()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var clientId = await CreateClientForRoleListAsync(administrator, "acme-dev");

        var owners = await GetJsonAsync(
            administrator,
            $"/api/tenants/acme-dev/clients/{clientId}/owners"
        );

        Assert.True(owners.GetProperty("canChange").GetBoolean());
    }

    [Fact]
    public async Task ListRoleAssignments_AsAnAdministrator_ReportsItMayChangeAResourceServersOwners()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTenantAsync(administrator, "acme", "acme-dev");
        var resourceServerId = await CreateResourceServerForRoleListAsync(
            administrator,
            "acme-dev"
        );

        var owners = await GetJsonAsync(
            administrator,
            $"/api/tenants/acme-dev/resource-servers/{resourceServerId}/owners"
        );

        Assert.True(owners.GetProperty("canChange").GetBoolean());
    }

    private static async Task<string> CreateClientForRoleListAsync(
        HttpClient client,
        string tenantId
    )
    {
        using var response = await client.PostAsJsonAsync(
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
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var clientId = JsonElement.Parse(body).GetProperty("clientId").GetString();
        Assert.NotNull(clientId);
        return clientId;
    }

    private static async Task<string> CreateResourceServerForRoleListAsync(
        HttpClient client,
        string tenantId
    )
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/resource-servers",
            new
            {
                identifier = "https://api.acme-dev.test",
                name = "Acme API",
                isDisabled = false,
                settings = new { },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var resourceServerId = JsonElement.Parse(body).GetProperty("resourceServerId").GetString();
        Assert.NotNull(resourceServerId);
        return resourceServerId;
    }

    #endregion

    #region PatchTeamSettings Tests

    [Fact]
    public async Task PatchTeamSettings_ReachesTheTeamsTenantsAtOnce()
    {
        using var factory = CreateFactory();
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
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
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
        await CreateTeamAsync(client, "globex", new { });
        await CreateTenantAsync(client, "globex", "globex-dev");
        await CreateTeamAsync(client, "hooli", new { });
        await CreateTenantAsync(client, "hooli", "hooli-dev");

        var globexAlgorithmsBefore = await GetIdTokenSigningAlgorithmsAsync(client, "globex-dev");
        Assert.Contains("PS256", globexAlgorithmsBefore);
        var hooliAlgorithmsBefore = await GetIdTokenSigningAlgorithmsAsync(client, "hooli-dev");
        Assert.Contains("PS256", hooliAlgorithmsBefore);

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

        var globexAlgorithmsAfter = await GetIdTokenSigningAlgorithmsAsync(client, "globex-dev");
        Assert.Equal<string>(["RS256"], globexAlgorithmsAfter);

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
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
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
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
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
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
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
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
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
        using var client = await factory.CreateAdministratorClientAsync(CancellationToken.None);
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
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
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
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection.Configure<TenantUsageOptions>(options =>
                    options.FlushInterval = TimeSpan.FromMilliseconds(50)
                )
            )
        );
        using var administrator = await host.CreateAdministratorClientAsync(CancellationToken.None);
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

    #region Promotion Tests

    [Fact]
    public async Task PreviewTenantPromotion_ReportsTheChangesAndNotesWithoutApplying()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var (clientId, resourceServerId) = await ArrangePromotionSourceAsync(administrator);

        using var previewResponse = await PostPromotionAsync(
            administrator,
            "acme",
            "/preview",
            "acme-dev",
            "acme-prod"
        );
        var preview = await ReadPromotionAsync(previewResponse, HttpStatusCode.OK);
        Assert.False(preview.GetProperty("isApplied").GetBoolean());
        Assert.True(HasChange(preview, "client", clientId, "created"), preview.ToString());
        Assert.True(
            HasChange(preview, "resource_server", resourceServerId, "created"),
            preview.ToString()
        );
        Assert.True(
            HasChange(preview, "client_grant", $"{clientId}/{resourceServerId}", "created"),
            preview.ToString()
        );
        Assert.True(HasNote(preview, clientId, "credential_required"), preview.ToString());
        Assert.True(
            HasNote(
                preview,
                clientId,
                "environment_setting_unset",
                OpenIdSettingNames.RedirectUris
            ),
            preview.ToString()
        );

        await AssertGetStatusAsync(
            administrator,
            $"/api/tenants/acme-prod/clients/{clientId}",
            HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task PromoteTenant_CopiesConfigurationButNoSecretsOrEnvironmentValues()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var (clientId, resourceServerId) = await ArrangePromotionSourceAsync(administrator);

        using var promoteResponse = await PostPromotionAsync(
            administrator,
            "acme",
            string.Empty,
            "acme-dev",
            "acme-prod"
        );
        var promoted = await ReadPromotionAsync(promoteResponse, HttpStatusCode.OK);
        Assert.True(promoted.GetProperty("isApplied").GetBoolean());

        var settings = (
            await GetJsonAsync(administrator, "/api/tenants/acme-prod/settings")
        ).GetProperty("settings");
        Assert.True(settings.GetProperty(OpenIdSettingNames.RequireConsent).GetBoolean());
        Assert.False(settings.TryGetProperty(OpenIdSettingNames.EnvironmentName, out _));

        var client = await GetJsonAsync(
            administrator,
            $"/api/tenants/acme-prod/clients/{clientId}"
        );
        Assert.Equal(clientId, client.GetProperty("clientId").GetString());
        var clientSettings = (
            await GetJsonAsync(administrator, $"/api/tenants/acme-prod/clients/{clientId}/settings")
        ).GetProperty("settings");
        Assert.Equal(
            "Acme Web",
            clientSettings.GetProperty(OpenIdSettingNames.ClientName).GetString()
        );
        Assert.False(clientSettings.TryGetProperty(OpenIdSettingNames.RedirectUris, out _));

        var secrets = await GetJsonAsync(
            administrator,
            $"/api/tenants/acme-prod/clients/{clientId}/secrets"
        );
        Assert.Empty(secrets.GetProperty("secrets").EnumerateArray());

        var grant = await GetJsonAsync(
            administrator,
            $"/api/tenants/acme-prod/clients/{clientId}/grants/{resourceServerId}"
        );
        Assert.Equal(
            ["read:things"],
            grant.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString())
        );
    }

    [Fact]
    public async Task PromoteTenant_WhenPromotedAgain_ReportsEveryChangeUnchanged()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await ArrangePromotionSourceAsync(administrator);

        using var promoteResponse = await PostPromotionAsync(
            administrator,
            "acme",
            string.Empty,
            "acme-dev",
            "acme-prod"
        );
        await ReadPromotionAsync(promoteResponse, HttpStatusCode.OK);

        using var againResponse = await PostPromotionAsync(
            administrator,
            "acme",
            string.Empty,
            "acme-dev",
            "acme-prod"
        );
        var again = await ReadPromotionAsync(againResponse, HttpStatusCode.OK);
        var actions = again
            .GetProperty("changes")
            .EnumerateArray()
            .Select(change => change.GetProperty("action").GetString())
            .ToList();
        Assert.NotEmpty(actions);
        Assert.All(actions, action => Assert.Equal("unchanged", action));
    }

    [Fact]
    public async Task PromoteTenant_WithEnvironmentSettings_WritesTheSuppliedValues()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var (clientId, _) = await ArrangePromotionSourceAsync(administrator);

        using var promoteResponse = await PostPromotionAsync(
            administrator,
            "acme",
            string.Empty,
            "acme-dev",
            "acme-prod",
            new[]
            {
                new
                {
                    resourceType = "client",
                    resourceId = clientId,
                    settings = new Dictionary<string, object>
                    {
                        [OpenIdSettingNames.RedirectUris] = new[] { "https://prod.acme.test/cb" },
                    },
                },
            }
        );
        await ReadPromotionAsync(promoteResponse, HttpStatusCode.OK);

        var clientSettings = (
            await GetJsonAsync(administrator, $"/api/tenants/acme-prod/clients/{clientId}/settings")
        ).GetProperty("settings");
        Assert.Equal(
            ["https://prod.acme.test/cb"],
            clientSettings
                .GetProperty(OpenIdSettingNames.RedirectUris)
                .EnumerateArray()
                .Select(uri => uri.GetString())
        );
    }

    [Fact]
    public async Task PromoteTenant_ByATeamOwner_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await ArrangePromotionSourceAsync(administrator);
        using var owner = await CreateTeamUserClientAsync(factory, administrator, "acme", "owners");

        foreach (var route in new[] { "/preview", string.Empty })
        {
            using var response = await PostPromotionAsync(
                owner,
                "acme",
                route,
                "acme-dev",
                "acme-prod"
            );
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, body);
        }
    }

    [Fact]
    public async Task PromoteTenant_ToAnotherTeamsTenant_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await ArrangePromotionSourceAsync(administrator);
        await CreateTeamAsync(administrator, "beta", new { });
        await CreateTenantAsync(administrator, "beta", "beta-prod");

        using var response = await PostPromotionAsync(
            administrator,
            "acme",
            string.Empty,
            "acme-dev",
            "beta-prod"
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NotFound, body);
    }

    [Fact]
    public async Task PromoteTenant_ByATenantAdminOfBoth_WhenALimitChanges_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await ArrangePromotionSourceAsync(administrator);
        await PatchAsync(
            administrator,
            "/api/tenants/acme-dev/settings",
            new[]
            {
                new
                {
                    op = "add",
                    path = $"/{OpenIdSettingNames.RateLimitTenantPerMinute}",
                    value = 100,
                },
            }
        );
        using var tenantAdmin = await CreateGrantedUserClientAsync(
            factory,
            administrator,
            "/api/tenants/acme-dev/admins"
        );
        var admins = await GetJsonAsync(administrator, "/api/tenants/acme-dev/admins");
        var principalId = admins
            .GetProperty("items")
            .EnumerateArray()
            .Select(admin => admin.GetProperty("principalId").GetString())
            .Single();
        using var grant = await administrator.PostAsJsonAsync(
            "/api/tenants/acme-prod/admins",
            new { principalId }
        );
        var grantBody = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Created, grantBody);

        foreach (var route in new[] { "/preview", string.Empty })
        {
            using var refused = await PostPromotionAsync(
                tenantAdmin,
                "acme",
                route,
                "acme-dev",
                "acme-prod"
            );
            var refusedBody = await refused.Content.ReadAsStringAsync();
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, refusedBody);
            Assert.Contains(
                OpenIdSettingNames.RateLimitTenantPerMinute,
                refusedBody,
                StringComparison.Ordinal
            );
        }

        using var allowed = await PostPromotionAsync(
            administrator,
            "acme",
            string.Empty,
            "acme-dev",
            "acme-prod"
        );
        await ReadPromotionAsync(allowed, HttpStatusCode.OK);
        var settings = (
            await GetJsonAsync(administrator, "/api/tenants/acme-prod/settings")
        ).GetProperty("settings");
        Assert.Equal(
            100,
            settings.GetProperty(OpenIdSettingNames.RateLimitTenantPerMinute).GetInt32()
        );
    }

    [Fact]
    public async Task PromoteTenant_LeavesTheHostManagedClientAlone()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var root = await GetJsonAsync(administrator, $"/api/tenants/{RootTenantId}");
        var teamId = root.GetProperty("teamId").GetString();
        Assert.NotNull(teamId);
        await CreateTenantAsync(administrator, teamId, "root-staging");

        using var response = await PostPromotionAsync(
            administrator,
            teamId,
            "/preview",
            RootTenantId,
            "root-staging"
        );

        var preview = await ReadPromotionAsync(response, HttpStatusCode.OK);
        Assert.True(
            HasNote(preview, BootstrapAdminApplicationFactory.ClientId, "host_managed"),
            preview.ToString()
        );
        Assert.DoesNotContain(
            preview.GetProperty("changes").EnumerateArray(),
            change =>
                change.GetProperty("resourceId").GetString()
                == BootstrapAdminApplicationFactory.ClientId
        );
    }

    #endregion

    #region Nested Team Tests

    [Fact]
    public async Task NestedTeam_ServesItsTenantsUnderItsParentsSettings()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(
            administrator,
            "globex",
            new { id_token_signing_alg_values_supported = new[] { "RS256" } }
        );
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        await CreateTenantAsync(administrator, "globex-web", "globex-web-dev");

        var signingAlgorithms = await GetIdTokenSigningAlgorithmsAsync(
            administrator,
            "globex-web-dev"
        );
        Assert.Equal<string>(["RS256"], signingAlgorithms);
    }

    [Fact]
    public async Task NestedTeam_PatchingTheParentsSettings_ReachesTheChildsTenantsAtOnce()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        await CreateTenantAsync(administrator, "globex-web", "globex-web-dev");

        var before = await GetIdTokenSigningAlgorithmsAsync(administrator, "globex-web-dev");
        Assert.Contains("PS256", before);

        await PatchAsync(
            administrator,
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
        var after = await GetIdTokenSigningAlgorithmsAsync(administrator, "globex-web-dev");
        Assert.Equal<string>(["RS256"], after);
    }

    [Fact]
    public async Task NestedTeam_DisablingTheParent_MakesTheChildsTenantsAnswerNotFound()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        await CreateTenantAsync(administrator, "globex-web", "globex-web-dev");

        using (var served = await GetDiscoveryAsync(administrator, "globex-web-dev"))
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        await SetTeamDisabledAsync(administrator, "globex", isDisabled: true);

        using (var silenced = await GetDiscoveryAsync(administrator, "globex-web-dev"))
            Assert.Equal(HttpStatusCode.NotFound, silenced.StatusCode);

        await SetTeamDisabledAsync(administrator, "globex", isDisabled: false);

        using (var restored = await GetDiscoveryAsync(administrator, "globex-web-dev"))
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task NestedTeam_DeletingAParentWithAChildTeam_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });

        using var response = await administrator.DeleteAsync("/api/teams/globex");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    [Fact]
    public async Task NestedTeam_ParentTeamAdmin_CreatesAChildTeamAndItsTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        using var parentAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "globex",
            "admins"
        );

        await CreateChildTeamAsync(parentAdmin, "globex", "globex-web", new { });
        await CreateTenantAsync(parentAdmin, "globex-web", "globex-web-dev");

        using var served = await GetDiscoveryAsync(administrator, "globex-web-dev");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task NestedTeam_ParentTeamAdmin_ChangesAChildTeamsLimits()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { max_tenants = 2 });
        using var parentAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "globex",
            "admins"
        );

        await PatchAsync(
            parentAdmin,
            "/api/teams/globex-web/settings",
            new[]
            {
                new
                {
                    op = "replace",
                    path = $"/{OpenIdSettingNames.MaxTenants}",
                    value = 5,
                },
            }
        );
    }

    [Fact]
    public async Task NestedTeam_ChildTeamAdmin_CannotChangeItsTeamsLimits()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { max_tenants = 2 });
        using var childAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "globex-web",
            "admins"
        );

        using var replaced = await SendPatchAsync(
            childAdmin,
            "/api/teams/globex-web/settings",
            new[]
            {
                new
                {
                    op = "replace",
                    path = $"/{OpenIdSettingNames.MaxTenants}",
                    value = 5,
                },
            }
        );

        var body = await replaced.Content.ReadAsStringAsync();
        Assert.True(replaced.StatusCode == HttpStatusCode.Forbidden, body);
        Assert.Contains(OpenIdSettingNames.MaxTenants, body);
    }

    [Fact]
    public async Task NestedTeam_CreatingATenantWhenTheParentIsFull_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { max_tenants = 1 });
        await CreateTenantAsync(administrator, "globex", "globex-main");
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });

        using var response = await PostTenantAsync(administrator, "globex-web", "globex-web-dev");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    [Fact]
    public async Task NestedTeam_BeneathThePlatformsOwnTeam_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await PostChildTeamAsync(
            administrator,
            HostApplicationFactory.TeamId,
            "under-platform",
            new { }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
    }

    [Fact]
    public async Task NestedTeam_Stranger_GetsForbiddenOnEveryChildTeamRouteForAnUnknownTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "acme", new { });
        await CreateTeamAsync(administrator, "globex", new { });
        using var stranger = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "acme",
            "admins"
        );

        foreach (var teamId in new[] { "nope", "globex" })
        {
            using var create = await PostChildTeamAsync(stranger, teamId, "stray", new { });
            var createBody = await create.Content.ReadAsStringAsync();
            Assert.True(create.StatusCode == HttpStatusCode.Forbidden, createBody);

            await AssertGetStatusAsync(
                stranger,
                $"/api/teams/{teamId}/teams",
                HttpStatusCode.Forbidden
            );
            await AssertGetStatusAsync(
                stranger,
                $"/api/teams/{teamId}/usage",
                HttpStatusCode.Forbidden
            );
        }
    }

    [Fact]
    public async Task NestedTeam_ChildTeamAdmin_GetsForbiddenForItsParentAndSiblings()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-api", new { });
        using var childAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "globex-web",
            "admins"
        );

        await AssertGetStatusAsync(childAdmin, "/api/teams/globex", HttpStatusCode.Forbidden);
        await AssertGetStatusAsync(childAdmin, "/api/teams/globex-api", HttpStatusCode.Forbidden);

        using var create = await PostChildTeamAsync(childAdmin, "globex", "stray", new { });
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Forbidden, createBody);

        await AssertGetStatusAsync(childAdmin, "/api/teams/globex-web", HttpStatusCode.OK);
    }

    [Fact]
    public async Task NestedTeam_ChildTeamAdmin_CannotDisableOrDeleteItsOwnTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        using var childAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "globex-web",
            "admins"
        );

        using var disabled = await SendPatchAsync(
            childAdmin,
            "/api/teams/globex-web",
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
        var disabledBody = await disabled.Content.ReadAsStringAsync();
        Assert.True(disabled.StatusCode == HttpStatusCode.Forbidden, disabledBody);

        using var deleted = await childAdmin.DeleteAsync("/api/teams/globex-web");
        var deletedBody = await deleted.Content.ReadAsStringAsync();
        Assert.True(deleted.StatusCode == HttpStatusCode.Forbidden, deletedBody);

        var team = await GetJsonAsync(administrator, "/api/teams/globex-web");
        Assert.False(team.GetProperty("isDisabled").GetBoolean(), team.ToString());
    }

    [Fact]
    public async Task NestedTeam_ParentTeamAdmin_DisablesRestoresAndDeletesAChildTeam()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        using var parentAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "globex",
            "admins"
        );

        // SetTeamDisabledAsync asserts 204 No Content.
        await SetTeamDisabledAsync(parentAdmin, "globex-web", isDisabled: true);
        await SetTeamDisabledAsync(parentAdmin, "globex-web", isDisabled: false);

        using var deleted = await parentAdmin.DeleteAsync("/api/teams/globex-web");
        var deletedBody = await deleted.Content.ReadAsStringAsync();
        Assert.True(deleted.StatusCode == HttpStatusCode.NoContent, deletedBody);
    }

    [Fact]
    public async Task NestedTeam_Administrator_GetsNotFoundForAnUnknownParent()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await PostChildTeamAsync(administrator, "nope", "stray", new { });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NotFound, body);
    }

    [Fact]
    public async Task NestedTeam_ParentUsage_ListsTheChildTeamsTenantsWithTheirTeam()
    {
        using var host = CreateFactory();
        using var recordingHost = host.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection.Configure<TenantUsageOptions>(options =>
                    options.FlushInterval = TimeSpan.FromMilliseconds(50)
                )
            )
        );
        using var administrator = await host.CreateAdministratorClientAsync(CancellationToken.None);
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        await CreateTenantAsync(administrator, "globex-web", "globex-web-dev");

        var client = recordingHost.CreateClient();
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
            request.Headers.Host = "globex-web-dev.auth.test";
            using var response = await client.SendAsync(request);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // The ledger is written by the periodic flush, so poll until the tenant's count arrives.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        JsonElement usage;
        while (true)
        {
            using var response = await administrator.GetAsync("/api/teams/globex/usage");
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            usage = JsonElement.Parse(body);
            if (GetCount(usage, "globex-web-dev") >= 2 || DateTime.UtcNow > deadline)
                break;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal("globex", usage.GetProperty("teamId").GetString());
        Assert.True(GetCount(usage, "globex-web-dev") >= 2, usage.ToString());
        Assert.All(
            usage
                .GetProperty("entries")
                .EnumerateArray()
                .Where(entry => entry.GetProperty("tenantId").GetString() == "globex-web-dev"),
            entry => Assert.Equal("globex-web", entry.GetProperty("teamId").GetString())
        );
    }

    [Fact]
    public async Task NestedTeam_ParentTeamAdmin_PromotesBetweenAChildTeamsTenants()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        await CreateTenantAsync(administrator, "globex-web", "globex-web-dev");
        await CreateTenantAsync(administrator, "globex-web", "globex-web-prod");
        using var parentAdmin = await CreateTeamUserClientAsync(
            factory,
            administrator,
            "globex",
            "admins"
        );

        using var response = await PostPromotionAsync(
            parentAdmin,
            "globex-web",
            "/preview",
            "globex-web-dev",
            "globex-web-prod"
        );

        await ReadPromotionAsync(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task NestedTeam_PromotingAChildTeamsTenantsThroughItsParent_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, "globex", new { });
        await CreateChildTeamAsync(administrator, "globex", "globex-web", new { });
        await CreateTenantAsync(administrator, "globex-web", "globex-web-dev");
        await CreateTenantAsync(administrator, "globex-web", "globex-web-prod");

        using var response = await PostPromotionAsync(
            administrator,
            "globex",
            "/preview",
            "globex-web-dev",
            "globex-web-prod"
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NotFound, body);
    }

    #endregion
}
