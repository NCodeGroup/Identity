// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Promotions;

public sealed class ClientGrantPromotionContributorTests : IDisposable
{
    private const string TeamId = "team-1";
    private const string SourceTenantId = "source-tenant";
    private const string TargetTenantId = "target-tenant";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IHostManagedClientChecker> MockHostManagedClientChecker { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IClientGrantStore> MockClientGrantStore { get; }
    private Mock<IResourceServerStore> MockResourceServerStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISupportTenantId> MockTeamNode { get; }
    private Mock<ISupportTenantId> MockTargetTenantNode { get; }

    public ClientGrantPromotionContributorTests()
    {
        MockHostManagedClientChecker = MockRepository.Create<IHostManagedClientChecker>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        MockClientGrantStore = MockRepository.Create<IClientGrantStore>();
        MockResourceServerStore = MockRepository.Create<IResourceServerStore>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTeamNode = MockRepository.Create<ISupportTenantId>();
        MockTargetTenantNode = MockRepository.Create<ISupportTenantId>();
    }

    public void Dispose() => MockRepository.Verify();

    private void SetupStores()
    {
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IClientGrantStore>())
            .Returns(MockClientGrantStore.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(MockResourceServerStore.Object)
            .Verifiable();
    }

    private void SetupSourceScope() => SetupScope(SourceTenantId);

    private void SetupTargetScope() => SetupScope(TargetTenantId);

    private void SetupScope(string tenantId)
    {
        var scope = MockRepository.Create<IDisposable>();
        scope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(tenantId))
            .Returns(scope.Object)
            .Verifiable();
    }

    private void SetupNoClientHostManaged() =>
        MockHostManagedClientChecker
            .Setup(x => x.IsHostManaged(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false)
            .Verifiable();

    private ClientGrantPromotionContributor CreateContributor() =>
        new(MockHostManagedClientChecker.Object);

    private static PersistedTenant CreateTenant(string tenantId) =>
        new PersistedTenantBuilder { TenantId = tenantId, TeamId = TeamId }.Build();

    private TenantPromotionContext CreateContext() =>
        new()
        {
            SourceTenant = CreateTenant(SourceTenantId),
            TargetTenant = CreateTenant(TargetTenantId),
            TeamNode = MockTeamNode.Object,
            TargetTenantNode = MockTargetTenantNode.Object,
            StoreManager = MockStoreManager.Object,
            AmbientTenantAccessor = MockAmbientTenantAccessor.Object,
            JsonSerializerOptions = new JsonSerializerOptions(),
            EnvironmentSettings = [],
            TargetTenantSettings = JsonElements.EmptyObject,
        };

    private static PersistedClient CreateClient(
        string tenantId,
        string clientId,
        string? owningNodeType = null
    ) =>
        new PersistedClientBuilder
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = "token",
            SettingsConcurrencyToken = "settings-token",
            SecretsConcurrencyToken = "secrets-token",
            PrincipalId = "principal-1",
            OwningNodeType = owningNodeType,
            OwningNodeId = owningNodeType is null ? null : "owner-1",
        }.Build();

    private static PersistedClientGrant CreateGrant(
        string tenantId,
        string clientId,
        string resourceServerId,
        params string[] scopes
    ) =>
        new()
        {
            TenantId = tenantId,
            ClientId = clientId,
            ResourceServerId = resourceServerId,
            ConcurrencyToken = "token",
            Scopes = scopes,
        };

    private static PersistedResourceServer CreateServer(
        string tenantId,
        string resourceServerId,
        string identifier,
        bool isSystem,
        params string[] scopes
    ) =>
        new PersistedResourceServerBuilder
        {
            TenantId = tenantId,
            ResourceServerId = resourceServerId,
            Identifier = identifier,
            Name = "Name",
            IsSystem = isSystem,
            Scopes =
            [
                .. scopes.Select(x => new PersistedScope
                {
                    Value = x,
                    Description = null,
                    IsSystem = isSystem,
                }),
            ],
        }.Build();

    private void SetupSourceClients(params PersistedClient[] clients) =>
        MockClientStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(request => request.Cursor == null && request.Limit == 100),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new PagedResult<PersistedClient> { Items = clients, NextCursor = null })
            .Verifiable();

    private void SetupSourceGrants(string clientId, params PersistedClientGrant[] grants) =>
        MockClientGrantStore
            .Setup(x =>
                x.GetPageAsync(
                    clientId,
                    It.Is<PageRequest>(request => request.Cursor == null && request.Limit == 100),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedClientGrant> { Items = grants, NextCursor = null }
            )
            .Verifiable();

    private void SetupTargetGrant(
        string clientId,
        string resourceServerId,
        PersistedClientGrant? target
    ) =>
        MockClientGrantStore
            .Setup(x => x.GetOrDefaultAsync(clientId, resourceServerId, CancellationToken.None))
            .ReturnsAsync(target)
            .Verifiable();

    private void SetupTargetResourceServer(
        string resourceServerId,
        PersistedResourceServer? resourceServer
    ) =>
        MockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync(resourceServerId, CancellationToken.None))
            .ReturnsAsync(resourceServer)
            .Verifiable();

    #region Order Property Tests

    [Fact]
    public void Order_Always_IsTheClientGrantsOrder()
    {
        Assert.Equal(TenantPromotionOrders.ClientGrants, CreateContributor().Order);
    }

    #endregion

    #region PlanAsync Tests

    [Fact]
    public async Task PlanAsync_WhenTheTargetLacksTheGrant_RecordsACreatedChange()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupTargetScope();
        SetupNoClientHostManaged();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants("client-1", CreateGrant(SourceTenantId, "client-1", "rs-source", "read"));
        SetupTargetGrant("client-1", "rs-target", null);
        SetupTargetResourceServer(
            "rs-target",
            CreateServer(TargetTenantId, "rs-target", "api", false, "read")
        );

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var change = Assert.Single(context.Changes);
        Assert.Equal(ManagementResourceTypes.ClientGrant, change.ResourceType);
        Assert.Equal("client-1/rs-target", change.ResourceId);
        Assert.Equal(TenantPromotionActions.Created, change.Action);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenTheScopeSetsDiffer_RecordsAnUpdatedChange()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupTargetScope();
        SetupNoClientHostManaged();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants(
            "client-1",
            CreateGrant(SourceTenantId, "client-1", "rs-source", "read", "write")
        );
        SetupTargetGrant(
            "client-1",
            "rs-target",
            CreateGrant(TargetTenantId, "client-1", "rs-target", "read")
        );
        SetupTargetResourceServer(
            "rs-target",
            CreateServer(TargetTenantId, "rs-target", "api", false, "read", "write")
        );

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Updated, Assert.Single(context.Changes).Action);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenTheScopeSetsMatchInAnyOrder_RecordsAnUnchangedChange()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupTargetScope();
        SetupNoClientHostManaged();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants(
            "client-1",
            CreateGrant(SourceTenantId, "client-1", "rs-source", "read", "write")
        );
        SetupTargetGrant(
            "client-1",
            "rs-target",
            CreateGrant(TargetTenantId, "client-1", "rs-target", "write", "read")
        );
        SetupTargetResourceServer("rs-target", null);

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(TenantPromotionActions.Unchanged, Assert.Single(context.Changes).Action);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenTheResourceServerIsNotMapped_RecordsAnErrorAndNoChange()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupNoClientHostManaged();
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants("client-1", CreateGrant(SourceTenantId, "client-1", "rs-source", "read"));

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        var error = Assert.Single(context.Errors);
        Assert.Equal(ManagementResourceTypes.ClientGrant, error.ResourceType);
        Assert.Equal("client-1/rs-source", error.ResourceId);
        Assert.Empty(context.Changes);
    }

    [Fact]
    public async Task PlanAsync_WhenTheTargetSystemResourceServerLacksAScope_RecordsAnErrorPerScope()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupTargetScope();
        SetupNoClientHostManaged();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants(
            "client-1",
            CreateGrant(SourceTenantId, "client-1", "rs-source", "read", "write", "admin")
        );
        SetupTargetGrant("client-1", "rs-target", null);
        SetupTargetResourceServer(
            "rs-target",
            CreateServer(TargetTenantId, "rs-target", "api", true, "read")
        );

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Equal(2, context.Errors.Count);
        Assert.All(
            context.Errors,
            error =>
            {
                Assert.Equal(ManagementResourceTypes.ClientGrant, error.ResourceType);
                Assert.Equal("client-1/rs-target", error.ResourceId);
            }
        );
        Assert.Contains(context.Errors, x => x.Detail.Contains("'write'"));
        Assert.Contains(context.Errors, x => x.Detail.Contains("'admin'"));
    }

    [Fact]
    public async Task PlanAsync_WhenTheClientIsHostManagedInTheSourceTenant_LeavesItsGrantsOut()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        MockHostManagedClientChecker
            .Setup(x => x.IsHostManaged(SourceTenantId, "client-1"))
            .Returns(true)
            .Verifiable();
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Changes);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenAClientIsAMachineClient_LeavesItsGrantsOut()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupSourceClients(
            CreateClient(SourceTenantId, "client-1", owningNodeType: ResourceNodeTypes.Team)
        );

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Changes);
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task PlanAsync_WhenTheClientIsHostManagedInTheTargetTenant_LeavesItsGrantsOut()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        MockHostManagedClientChecker
            .Setup(x => x.IsHostManaged(SourceTenantId, "client-1"))
            .Returns(false)
            .Verifiable();
        MockHostManagedClientChecker
            .Setup(x => x.IsHostManaged(TargetTenantId, "client-1"))
            .Returns(true)
            .Verifiable();
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));

        await CreateContributor().PlanAsync(context, CancellationToken.None);

        Assert.Empty(context.Changes);
        Assert.Empty(context.Errors);
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_WhenTheTargetLacksTheGrant_AddsTheGrantWithTheMappedResourceServer()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupTargetScope();
        SetupNoClientHostManaged();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants(
            "client-1",
            CreateGrant(SourceTenantId, "client-1", "rs-source", "read", "write")
        );
        SetupTargetGrant("client-1", "rs-target", null);
        SetupTargetResourceServer("rs-target", null);
        MockClientGrantStore
            .Setup(x =>
                x.AddAsync(
                    It.Is<PersistedClientGrant>(grant =>
                        grant.TenantId == TargetTenantId
                        && grant.ClientId == "client-1"
                        && grant.ResourceServerId == "rs-target"
                        && grant.ConcurrencyToken == string.Empty
                        && grant.Scopes.SequenceEqual(new[] { "read", "write" })
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheScopeSetsDiffer_ReplacesTheTargetScopes()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupTargetScope();
        SetupNoClientHostManaged();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        var target = CreateGrant(TargetTenantId, "client-1", "rs-target", "read");
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants(
            "client-1",
            CreateGrant(SourceTenantId, "client-1", "rs-source", "read", "write")
        );
        SetupTargetGrant("client-1", "rs-target", target);
        SetupTargetResourceServer("rs-target", null);
        MockClientGrantStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedClientGrant>(grant =>
                        ReferenceEquals(grant, target)
                        && grant.Scopes.SequenceEqual(new[] { "read", "write" })
                    ),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateContributor().ApplyAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheScopeSetsMatch_WritesNothing()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        SetupTargetScope();
        SetupNoClientHostManaged();
        context.MapIdentifier(ManagementResourceTypes.ResourceServer, "rs-source", "rs-target");
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));
        SetupSourceGrants("client-1", CreateGrant(SourceTenantId, "client-1", "rs-source", "read"));
        SetupTargetGrant(
            "client-1",
            "rs-target",
            CreateGrant(TargetTenantId, "client-1", "rs-target", "read")
        );
        SetupTargetResourceServer("rs-target", null);

        await CreateContributor().ApplyAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheClientIsHostManaged_WritesNothing()
    {
        var context = CreateContext();
        SetupStores();
        SetupSourceScope();
        MockHostManagedClientChecker
            .Setup(x => x.IsHostManaged(SourceTenantId, "client-1"))
            .Returns(true)
            .Verifiable();
        SetupSourceClients(CreateClient(SourceTenantId, "client-1"));

        await CreateContributor().ApplyAsync(context, CancellationToken.None);
    }

    #endregion
}
