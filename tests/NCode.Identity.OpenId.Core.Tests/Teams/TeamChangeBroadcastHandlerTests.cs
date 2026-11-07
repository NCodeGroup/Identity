// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class TeamChangeBroadcastHandlerTests : IDisposable
{
    private const string TeamId = "platform";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IOpenIdTeamCache> MockTeamCache { get; }
    private Mock<IOpenIdTenantCache> MockTenantCache { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private TeamChangeBroadcastHandler Handler { get; }

    public TeamChangeBroadcastHandlerTests()
    {
        MockTeamCache = Mocks.Create<IOpenIdTeamCache>();
        MockTenantCache = Mocks.Create<IOpenIdTenantCache>();
        MockStoreManagerFactory = Mocks.Create<IStoreManagerFactory>();
        MockStoreManager = Mocks.Create<IStoreManager>();
        MockTeamStore = Mocks.Create<ITeamStore>();
        Handler = new TeamChangeBroadcastHandler(
            MockTeamCache.Object,
            MockTenantCache.Object,
            MockStoreManagerFactory.Object,
            NullLogger<TeamChangeBroadcastHandler>.Instance
        );
    }

    public void Dispose()
    {
        Mocks.Verify();
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheTeamChangedChannel()
    {
        Assert.Equal("team-changed", Handler.Channel);
        Assert.Equal(TeamChangeBroadcastHandler.ChannelName, Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_EvictsTheAnnouncedTeamAndItsTenants()
    {
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(TeamId, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheTeamRemovalFails_StillEvictsTheTenantsAndThrows()
    {
        var failure = new InvalidOperationException("The removal failed.");
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(() => ValueTask.FromException(failure))
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(TeamId, CancellationToken.None)
        );

        Assert.Same(failure, thrown);
    }

    #endregion

    #region HandleMissedMessagesAsync Tests

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenNothingIsCached_ReadsNoStore()
    {
        SetupRevisions([], []);

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_EvictsOnlyTheTeamsWhoseRowChangedOrIsGone()
    {
        SetupRevisions(
            [new TeamRevision("acme", "a1"), new TeamRevision("globex", "g1")],
            [new TeamRevision("initech", "i1"), new TeamRevision("acme", "a1")]
        );
        SetupStore(
            ids => Assert.Equal(3, ids.Count),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["acme"] = "a1",
                ["globex"] = "g2",
            }
        );
        SetupEviction("globex");
        SetupEviction("initech");

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenTheTenantsWereMaterializedUnderAnOlderRevision_EvictsTheTeam()
    {
        SetupRevisions([new TeamRevision("acme", "a1")], [new TeamRevision("acme", "a0")]);
        SetupStore(
            ids => Assert.Equal(["acme"], ids),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["acme"] = "a1" }
        );
        SetupEviction("acme");

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_ReadsTheTokensInBatches()
    {
        var revisions = Enumerable
            .Range(0, TeamChangeBroadcastHandler.TeamBatchSize + 1)
            .Select(index => new TeamRevision($"team-{index}", "t1"))
            .ToList();
        SetupRevisions(revisions, []);
        var batchSizes = new List<int>();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTeamStore
            .Setup(x =>
                x.GetConcurrencyTokensAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (IReadOnlyCollection<string> ids, CancellationToken _) => batchSizes.Add(ids.Count)
            )
            .ReturnsAsync(
                (IReadOnlyCollection<string> ids, CancellationToken _) =>
                    (IReadOnlyDictionary<string, string>)
                        ids.ToDictionary(id => id, _ => "t1", StringComparer.OrdinalIgnoreCase)
            )
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);

        Assert.Equal([TeamChangeBroadcastHandler.TeamBatchSize, 1], batchSizes);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenTheTokensCannotBeRead_ClearsBothCaches()
    {
        SetupRevisions([new TeamRevision("acme", "a1")], []);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable();
        SetupClear(MockTeamCache);
        SetupClear(MockTenantCache);

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenReadingTheTokensTimesOut_ClearsBothCaches()
    {
        SetupRevisions([new TeamRevision("acme", "a1")], []);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException())
            .Verifiable();
        SetupClear(MockTeamCache);
        SetupClear(MockTenantCache);

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenTheTokensCannotBeReadAndTheTeamCacheClearFails_StillClearsTheTenantCache()
    {
        MockTeamCache
            .Setup(x =>
                x.GetRevisionsAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("The read failed."))
            .Verifiable();
        MockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The clear failed."))
            .Verifiable();
        SetupClear(MockTenantCache);

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenTheTokensCannotBeReadAndBothClearsFail_LogsAndCompletes()
    {
        SetupRevisions([new TeamRevision("acme", "a1")], []);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable();
        MockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The team clear failed."))
            .Verifiable();
        MockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The tenant clear failed."))
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenAnEvictionFails_EvictsTheOtherStaleTeamsAndClearsBothCaches()
    {
        SetupRevisions([new TeamRevision("acme", "a1"), new TeamRevision("globex", "g1")], []);
        SetupStore(
            ids => Assert.Equal(["acme", "globex"], ids),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["acme"] = "a2",
                ["globex"] = "g2",
            }
        );
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync("acme", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(() => ValueTask.FromException(new InvalidOperationException("Failed.")))
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync("acme", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupEviction("globex");
        SetupClear(MockTeamCache);
        SetupClear(MockTenantCache);

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    #endregion

    private void SetupRevisions(
        IReadOnlyCollection<TeamRevision> teamRevisions,
        IReadOnlyCollection<TeamRevision> tenantRevisions
    )
    {
        MockTeamCache
            .Setup(x =>
                x.GetRevisionsAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(teamRevisions)
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.GetTeamRevisionsAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(tenantRevisions)
            .Verifiable();
    }

    private void SetupStore(
        Action<IReadOnlyCollection<string>> inspectIds,
        IReadOnlyDictionary<string, string> tokens
    )
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTeamStore
            .Setup(x =>
                x.GetConcurrencyTokensAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (IReadOnlyCollection<string> ids, CancellationToken _) =>
                    inspectIds(ids.Order(StringComparer.OrdinalIgnoreCase).ToList())
            )
            .ReturnsAsync(tokens)
            .Verifiable();
    }

    private void SetupEviction(string teamId)
    {
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(teamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync(teamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private static void SetupClear(Mock<IOpenIdTeamCache> mockCache) =>
        mockCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private static void SetupClear(Mock<IOpenIdTenantCache> mockCache) =>
        mockCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
}
