// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// Every role assignment the management API grants or revokes is audited as role_assignment.created or
// role_assignment.deleted, naming the acting administrator or client (ADR-1465).
public class RoleAssignmentAuditTests
{
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string ControlPlaneTenantId = "root";
    private const string TeamId = "acme";
    private const string TenantId = "acme-dev";
    private const string ChildTeamId = "acme-labs";

    #region CreateTeam Tests

    // A machine client holds roles only within its owning node (ADR-1466), so the creator is the team's machine client
    // and the team it creates sits beneath its owning team.
    [Fact]
    public async Task CreateTeam_RecordsTheCreatorsOwnerAndTeamAdminAssignments()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, CancellationToken.None);
        var (clientId, clientSecret, principalId) = await CreateTeamMachineClientAsync(
            administrator,
            CancellationToken.None
        );
        using var grant = await PostAdminAsync(administrator, principalId, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        using var machine = await factory.CreateManagementClientAsync(
            clientId,
            clientSecret,
            CancellationToken.None
        );

        await CreateChildTeamAsync(machine, CancellationToken.None);

        var changes = GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, ChildTeamId);
        Assert.All(
            changes,
            change =>
            {
                Assert.Equal(ResourceChangeTypes.Created, change.ChangeType);
                Assert.Equal(ControlPlaneTenantId, change.TenantId);
            }
        );
        Assert.Contains(changes, change => GetRoleName(change) == BuiltInRoles.Owner);
        Assert.Contains(changes, change => GetRoleName(change) == BuiltInRoles.TeamAdmin);
        Assert.All(changes, change => Assert.Equal(principalId, change.SubjectId));
    }

    #endregion

    #region AddTeamAdmin Tests

    [Fact]
    public async Task AddTeamAdmin_RecordsACreatedRoleAssignmentNamingTheActor()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, CancellationToken.None);
        var (_, _, principalId) = await CreateTeamMachineClientAsync(
            administrator,
            CancellationToken.None
        );

        using var response = await PostAdminAsync(
            administrator,
            principalId,
            CancellationToken.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var changes = GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, TeamId);
        var change = Assert.Single(changes, change => change.SubjectId == principalId);
        Assert.Equal("role_assignment.created", change.EventName);
        Assert.Equal(BuiltInRoles.TeamAdmin, GetRoleName(change));
        Assert.Equal(principalId, GetString(change, "principalId"));
        Assert.Collection(
            change.Resources,
            resource =>
            {
                Assert.Equal(ManagementResourceTypes.RoleAssignment, resource.Type);
                Assert.Equal(change.ResourceId, resource.Id);
            },
            resource =>
            {
                Assert.Equal(ManagementResourceTypes.Team, resource.Type);
                Assert.Equal(TeamId, resource.Id);
            }
        );
        Assert.Equal(AuditActorTypes.Client, change.Actor.Type);
        Assert.Equal(BootstrapAdminApplicationFactory.ClientId, change.Actor.Id);
    }

    [Fact]
    public async Task AddTeamAdmin_WhenAlreadyHeld_ReturnsOkAndRecordsNothing()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, CancellationToken.None);
        var (_, _, principalId) = await CreateTeamMachineClientAsync(
            administrator,
            CancellationToken.None
        );
        using var first = await PostAdminAsync(administrator, principalId, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var changesBefore = GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, TeamId);
        var recorded = changesBefore.Count;

        using var second = await PostAdminAsync(administrator, principalId, CancellationToken.None);

        var body = await second.Content.ReadAsStringAsync();
        Assert.True(second.StatusCode == HttpStatusCode.OK, body);
        var changes = GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, TeamId);
        Assert.Equal(recorded, changes.Count);
    }

    #endregion

    #region RemoveTenantOwner Tests

    [Fact]
    public async Task RemoveTenantOwner_RecordsADeletedRoleAssignment()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await CreateTeamAsync(administrator, CancellationToken.None);
        await CreateTenantAsync(administrator, CancellationToken.None);
        // The resource keeps at least one owner, so a second one holds it while the first is revoked.
        var (_, _, keptPrincipalId) = await CreateTeamMachineClientAsync(
            administrator,
            CancellationToken.None
        );
        await PostTenantOwnerAsync(administrator, keptPrincipalId, CancellationToken.None);
        var (_, _, principalId) = await CreateTeamMachineClientAsync(
            administrator,
            CancellationToken.None
        );
        await PostTenantOwnerAsync(administrator, principalId, CancellationToken.None);

        using var revoke = await administrator.DeleteAsync(
            $"/api/tenants/{TenantId}/owners/{principalId}"
        );

        var revokeBody = await revoke.Content.ReadAsStringAsync();
        Assert.True(revoke.StatusCode == HttpStatusCode.NoContent, revokeBody);
        var changes = GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Tenant, TenantId);
        var change = Assert.Single(
            changes,
            change => change.ChangeType == ResourceChangeTypes.Deleted
        );
        Assert.Equal("role_assignment.deleted", change.EventName);
        Assert.Equal(principalId, change.SubjectId);
        Assert.Equal(BuiltInRoles.Owner, GetRoleName(change));
        Assert.Equal(BootstrapAdminApplicationFactory.ClientId, change.Actor.Id);
    }

    #endregion

    #region Scaffolding

    private static List<RoleAssignmentChangedAuditEvent> GetRoleAssignmentChanges(
        RecordingAuditEventHandler recorder,
        string nodeType,
        string nodeId
    ) =>
        recorder
            .Events.OfType<RoleAssignmentChangedAuditEvent>()
            .Where(change => change.NodeType == nodeType && change.NodeId == nodeId)
            .ToList();

    private static string? GetRoleName(RoleAssignmentChangedAuditEvent change) =>
        GetString(change, "roleName");

    private static string? GetString(RoleAssignmentChangedAuditEvent change, string name) =>
        change.ResourceValues is { } values
        && values.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async ValueTask CreateTeamAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                teamId = TeamId,
                displayName = TeamId,
                isDisabled = false,
                settings = new { },
            },
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    private static async ValueTask CreateChildTeamAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/teams/{TeamId}/teams",
            new
            {
                teamId = ChildTeamId,
                displayName = ChildTeamId,
                isDisabled = false,
                settings = new { },
            },
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    private static async ValueTask CreateTenantAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/teams/{TeamId}/tenants",
            new
            {
                tenantId = TenantId,
                displayName = TenantId,
                isDisabled = false,
                settings = new { },
            },
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    // Creates a machine client of the team, which authenticates with HTTP Basic, and returns its credentials and the
    // principal that its tokens resolve to.
    private static async ValueTask<(
        string ClientId,
        string ClientSecret,
        string PrincipalId
    )> CreateTeamMachineClientAsync(HttpClient administrator, CancellationToken cancellationToken)
    {
        using var created = await administrator.PostAsJsonAsync(
            $"/api/teams/{TeamId}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretBasic,
                },
            },
            cancellationToken
        );
        var createdBody = await created.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
        var createdClient = JsonElement.Parse(createdBody);
        var clientId = createdClient.GetProperty("clientId").GetString();
        Assert.NotNull(clientId);
        var principalId = createdClient.GetProperty("principalId").GetString();
        Assert.NotNull(principalId);

        using var secret = await administrator.PostAsJsonAsync(
            $"/api/tenants/{ControlPlaneTenantId}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            },
            cancellationToken
        );
        var secretBody = await secret.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(secret.StatusCode == HttpStatusCode.Created, secretBody);
        var material = JsonElement.Parse(secretBody).GetProperty("secretMaterial").GetString();
        Assert.NotNull(material);
        return (clientId, material, principalId);
    }

    private static async ValueTask PostTenantOwnerAsync(
        HttpClient administrator,
        string principalId,
        CancellationToken cancellationToken
    )
    {
        using var grant = await administrator.PostAsJsonAsync(
            $"/api/tenants/{TenantId}/owners",
            new { principalId },
            cancellationToken
        );
        var body = await grant.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(grant.StatusCode == HttpStatusCode.Created, body);
    }

    private static async ValueTask<HttpResponseMessage> PostAdminAsync(
        HttpClient administrator,
        string principalId,
        CancellationToken cancellationToken
    ) =>
        await administrator.PostAsJsonAsync(
            $"/api/teams/{TeamId}/admins",
            new { principalId },
            cancellationToken
        );

    /// <summary>
    /// A factory that selects the tenant by host, with the bootstrap administrator in the control-plane tenant and an
    /// audit recorder registered.
    /// </summary>
    private sealed class RoleAssignmentAuditHostApplicationFactory(
        RecordingAuditEventHandler recorder
    )
        : BootstrapAdminApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByHost,
                ["OpenId:TenantResolution:Root:Issuer"] = RootIssuer,
                ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = IssuerTemplate,
            },
            ControlPlaneTenantId
        )
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(serviceCollection =>
                serviceCollection.AddSingleton<IEventHandler<IAuditEvent>>(recorder)
            );
        }
    }

    #endregion
}
