// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// Every role assignment the management API grants or revokes is audited as role_assignment.created or
// role_assignment.deleted, naming the acting administrator or client (ADR-1449).
public class RoleAssignmentAuditTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string ControlPlaneTenantId = "root";
    private const string TeamId = "acme";
    private const string TenantId = "acme-dev";

    [Fact]
    public async Task CreateTeam_RecordsTheCreatorsOwnerAndTeamAdminAssignments()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await CreateAdministratorClientAsync(factory);

        await CreateTeamAsync(administrator);

        var changes = GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, TeamId);
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
        Assert.Single(changes.Select(change => change.SubjectId).Distinct());
    }

    [Fact]
    public async Task AddTeamAdmin_RecordsACreatedRoleAssignmentNamingTheActor()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator);
        var principalId = await CreateMachineClientPrincipalAsync(administrator);

        using var response = await PostAdminAsync(administrator, principalId);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var change = Assert.Single(
            GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, TeamId),
            change => change.SubjectId == principalId
        );
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
        Assert.Equal(BootstrapClientId, change.Actor.Id);
    }

    [Fact]
    public async Task AddTeamAdmin_WhenAlreadyHeld_ReturnsOkAndRecordsNothing()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator);
        var principalId = await CreateMachineClientPrincipalAsync(administrator);
        using var first = await PostAdminAsync(administrator, principalId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var recorded = GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, TeamId).Count;

        using var second = await PostAdminAsync(administrator, principalId);

        var body = await second.Content.ReadAsStringAsync();
        Assert.True(second.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal(
            recorded,
            GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Team, TeamId).Count
        );
    }

    [Fact]
    public async Task RemoveTenantOwner_RecordsADeletedRoleAssignment()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new RoleAssignmentAuditHostApplicationFactory(recorder);
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTeamAsync(administrator);
        await CreateTenantAsync(administrator);
        var principalId = await CreateMachineClientPrincipalAsync(administrator);
        using var grant = await administrator.PostAsJsonAsync(
            $"/api/tenants/{TenantId}/owners",
            new { principalId }
        );
        var grantBody = await grant.Content.ReadAsStringAsync();
        Assert.True(grant.StatusCode == HttpStatusCode.Created, grantBody);

        using var revoke = await administrator.DeleteAsync(
            $"/api/tenants/{TenantId}/owners/{principalId}"
        );

        var revokeBody = await revoke.Content.ReadAsStringAsync();
        Assert.True(revoke.StatusCode == HttpStatusCode.NoContent, revokeBody);
        var change = Assert.Single(
            GetRoleAssignmentChanges(recorder, ResourceNodeTypes.Tenant, TenantId),
            change => change.ChangeType == ResourceChangeTypes.Deleted
        );
        Assert.Equal("role_assignment.deleted", change.EventName);
        Assert.Equal(principalId, change.SubjectId);
        Assert.Equal(BuiltInRoles.Owner, GetRoleName(change));
        Assert.Equal(BootstrapClientId, change.Actor.Id);
    }

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

    private static Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    ) => factory.CreateManagementClientAsync(BootstrapClientId, BootstrapClientSecret);

    private static async Task CreateTeamAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                teamId = TeamId,
                displayName = TeamId,
                isDisabled = false,
                settings = new { },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    private static async Task CreateTenantAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/teams/{TeamId}/tenants",
            new
            {
                tenantId = TenantId,
                displayName = TenantId,
                isDisabled = false,
                settings = new { },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    // Creates a client in the control-plane tenant and returns the principal that its tokens resolve to.
    private static async Task<string> CreateMachineClientPrincipalAsync(HttpClient administrator)
    {
        using var created = await administrator.PostAsJsonAsync(
            $"/api/tenants/{ControlPlaneTenantId}/clients",
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
        return JsonElement.Parse(createdBody).GetProperty("principalId").GetString()
            ?? throw new InvalidOperationException("The response has no principalId.");
    }

    private static Task<HttpResponseMessage> PostAdminAsync(
        HttpClient administrator,
        string principalId
    ) => administrator.PostAsJsonAsync($"/api/teams/{TeamId}/admins", new { principalId });

    /// <summary>
    /// A factory that selects the tenant by host, with the bootstrap administrator in the control-plane tenant and an
    /// audit recorder registered.
    /// </summary>
    private sealed class RoleAssignmentAuditHostApplicationFactory(
        RecordingAuditEventHandler recorder
    ) : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(
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
                        ["BootstrapAdmin:TenantId"] = ControlPlaneTenantId,
                    }
                )
            );
            builder.ConfigureServices(services =>
                services.AddSingleton<IEventHandler<IAuditEvent>>(recorder)
            );
        }
    }

    #endregion
}
