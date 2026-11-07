// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management;

public class DefaultAuthorityChainResolverTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private const string TeamId = "Team-1";
    private const string ParentTeamId = "Parent-Team";
    private const string TenantId = "Tenant-1";

    private DefaultAuthorityChainResolver Resolver { get; } = new();

    private static PersistedTenant CreateTenant(string teamId) =>
        new()
        {
            TenantId = TenantId,
            TeamId = teamId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = default,
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private static PersistedTeam CreateTeam(string teamId, string[] ancestorTeamIds) =>
        new()
        {
            TeamId = teamId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            DisplayName = teamId,
            AncestorTeamIds = ancestorTeamIds,
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = string.Empty,
                Value = default,
            },
        };

    private Mock<IStoreManager> CreateStoreManager(
        PersistedTenant? tenant,
        PersistedTeam? team,
        bool readsTenant = true
    )
    {
        var manager = MockRepository.Create<IStoreManager>();

        if (readsTenant)
        {
            var tenantStore = MockRepository.Create<ITenantStore>();
            manager.Setup(x => x.GetStore<ITenantStore>()).Returns(tenantStore.Object).Verifiable();
            tenantStore
                .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant)
                .Verifiable();
        }

        if (team is not null)
        {
            var teamStore = MockRepository.Create<ITeamStore>();
            manager.Setup(x => x.GetStore<ITeamStore>()).Returns(teamStore.Object).Verifiable();
            teamStore
                .Setup(x => x.GetOrDefaultAsync(team.TeamId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(team)
                .Verifiable();
        }

        return manager;
    }

    #region GetAncestorNodesAsync Tests

    [Fact]
    public async Task GetAncestorNodesAsync_ForATenantNode_ReturnsThePlatformTheTenantItsTeamAndTheTeamsAbove()
    {
        var manager = CreateStoreManager(CreateTenant(TeamId), CreateTeam(TeamId, [ParentTeamId]));
        var node = ResourceNode.For(TenantId, ResourceNodeTypes.Tenant, TenantId);

        var nodes = await Resolver.GetAncestorNodesAsync(
            node,
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(4, nodes.Count);
        Assert.Contains((ResourceNodeTypes.Platform, "platform"), nodes);
        Assert.Contains((ResourceNodeTypes.Tenant, "tenant-1"), nodes);
        Assert.Contains((ResourceNodeTypes.Team, "team-1"), nodes);
        Assert.Contains((ResourceNodeTypes.Team, "parent-team"), nodes);
    }

    [Fact]
    public async Task GetAncestorNodesAsync_ForAnUnknownTenant_AddsNoTeam()
    {
        var manager = CreateStoreManager(null, null);
        var node = ResourceNode.For(TenantId, ResourceNodeTypes.Tenant, TenantId);

        var nodes = await Resolver.GetAncestorNodesAsync(
            node,
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(2, nodes.Count);
        Assert.Contains((ResourceNodeTypes.Platform, "platform"), nodes);
        Assert.Contains((ResourceNodeTypes.Tenant, "tenant-1"), nodes);
    }

    [Fact]
    public async Task GetAncestorNodesAsync_ForATeamNode_ReturnsThePlatformTheTeamAndTheTeamsAbove()
    {
        var manager = CreateStoreManager(
            null,
            CreateTeam(TeamId, [ParentTeamId]),
            readsTenant: false
        );
        var node = ResourceNode.For(TenantId, ResourceNodeTypes.Team, TeamId);

        var nodes = await Resolver.GetAncestorNodesAsync(
            node,
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(3, nodes.Count);
        Assert.Contains((ResourceNodeTypes.Platform, "platform"), nodes);
        Assert.Contains((ResourceNodeTypes.Team, "team-1"), nodes);
        Assert.Contains((ResourceNodeTypes.Team, "parent-team"), nodes);
    }

    [Fact]
    public async Task GetAncestorNodesAsync_ForAClientNode_ReturnsItsTenantsChainWithoutTheClient()
    {
        var manager = CreateStoreManager(CreateTenant(TeamId), CreateTeam(TeamId, [ParentTeamId]));
        var node = ResourceNode.For(TenantId, ResourceNodeTypes.Client, "client-1");

        var nodes = await Resolver.GetAncestorNodesAsync(
            node,
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(4, nodes.Count);
        Assert.Contains((ResourceNodeTypes.Platform, "platform"), nodes);
        Assert.Contains((ResourceNodeTypes.Tenant, "tenant-1"), nodes);
        Assert.Contains((ResourceNodeTypes.Team, "team-1"), nodes);
        Assert.Contains((ResourceNodeTypes.Team, "parent-team"), nodes);
        Assert.DoesNotContain((ResourceNodeTypes.Client, "client-1"), nodes);
    }

    #endregion
}
