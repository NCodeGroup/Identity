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
using NCode.Identity.OpenId.Management.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A machine client that deletes itself is still audited as the client that acted (ADR-1450, ADR-1470).
public class MachineClientAuditTests
{
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string ControlPlaneTenantId = "root";
    private const string TeamId = "acme";

    #region DeleteClient Tests

    [Fact]
    public async Task DeleteClient_WhenAMachineClientDeletesItself_RecordsTheClientActor()
    {
        var recorder = new RecordingAuditEventHandler();
        using var factory = new MachineClientAuditHostApplicationFactory(recorder);
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

        using var response = await machine.DeleteAsync(
            $"/api/tenants/{ControlPlaneTenantId}/clients/{clientId}"
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);
        var changes = recorder
            .Events.OfType<ClientChangedAuditEvent>()
            .Where(x => x.ChangeType == ResourceChangeTypes.Deleted && x.ResourceId == clientId)
            .ToList();
        var change = Assert.Single(changes);
        Assert.Equal(AuditActorTypes.Client, change.Actor.Type);
        Assert.Equal(clientId, change.Actor.Id);
    }

    #endregion

    #region Scaffolding

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
    private sealed class MachineClientAuditHostApplicationFactory(
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
