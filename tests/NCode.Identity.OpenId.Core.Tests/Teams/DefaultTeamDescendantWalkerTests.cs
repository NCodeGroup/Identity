// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Teams;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Teams;

public sealed class DefaultTeamDescendantWalkerTests : BaseTests
{
    private const string TeamId = "acme";
    private const string WebTeamId = "web";
    private const string WebEuTeamId = "web-eu";
    private const string ApiTeamId = "api";
    private const string NextCursor = "next";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private DefaultTeamDescendantWalker Walker { get; }

    // The teams the walk visited, in visit order.
    private List<string> Visited { get; } = [];

    public DefaultTeamDescendantWalkerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTeamStore = CreateStrictMock<ITeamStore>();

        Walker = new DefaultTeamDescendantWalker(MockStoreManagerFactory.Object);
    }

    #region WalkDescendantsAsync Tests

    [Fact]
    public async Task WalkDescendantsAsync_VisitsEveryLevelWithEachParentBeforeItsChildren()
    {
        SetupStoreManager(4);
        SetupChildPage(TeamId, null, [WebTeamId, ApiTeamId], null);
        SetupChildPage(WebTeamId, null, [WebEuTeamId], null);
        SetupChildPage(WebEuTeamId, null, [], null);
        SetupChildPage(ApiTeamId, null, [], null);

        await Walker.WalkDescendantsAsync(TeamId, VisitAndDescend, CancellationToken.None);

        Assert.Equal([WebTeamId, WebEuTeamId, ApiTeamId], Visited);
    }

    [Fact]
    public async Task WalkDescendantsAsync_FollowsTheCursorAcrossPages()
    {
        SetupStoreManager(4);
        SetupChildPage(TeamId, null, [WebTeamId], NextCursor);
        SetupChildPage(WebTeamId, null, [], null);
        SetupChildPage(TeamId, NextCursor, [ApiTeamId], null);
        SetupChildPage(ApiTeamId, null, [], null);

        await Walker.WalkDescendantsAsync(TeamId, VisitAndDescend, CancellationToken.None);

        Assert.Equal([WebTeamId, ApiTeamId], Visited);
    }

    [Fact]
    public async Task WalkDescendantsAsync_WhenTheVisitDeclinesATeam_ReadsNothingBeneathIt()
    {
        SetupStoreManager(2);
        SetupChildPage(TeamId, null, [WebTeamId, ApiTeamId], null);
        SetupChildPage(ApiTeamId, null, [], null);

        await Walker.WalkDescendantsAsync(
            TeamId,
            (team, _) =>
            {
                Visited.Add(team.TeamId);
                return ValueTask.FromResult(team.TeamId != WebTeamId);
            },
            CancellationToken.None
        );

        Assert.Equal([WebTeamId, ApiTeamId], Visited);
    }

    [Fact]
    public async Task WalkDescendantsAsync_WhenTheTeamHasNoChildren_VisitsNothing()
    {
        SetupStoreManager(1);
        SetupChildPage(TeamId, null, [], null);

        await Walker.WalkDescendantsAsync(TeamId, VisitAndDescend, CancellationToken.None);

        Assert.Empty(Visited);
    }

    [Fact]
    public async Task WalkDescendantsAsync_ReadsEachPageInItsOwnUnitOfWork()
    {
        SetupStoreManager(3);
        SetupChildPage(TeamId, null, [WebTeamId], NextCursor);
        SetupChildPage(WebTeamId, null, [], null);
        SetupChildPage(TeamId, NextCursor, [], null);

        await Walker.WalkDescendantsAsync(TeamId, VisitAndDescend, CancellationToken.None);

        Assert.Equal([WebTeamId], Visited);
    }

    #endregion

    private ValueTask<bool> VisitAndDescend(PersistedTeam team, CancellationToken _)
    {
        Visited.Add(team.TeamId);
        return ValueTask.FromResult(true);
    }

    // Each page read opens one unit of work and disposes it.
    private void SetupStoreManager(int pageReads)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable(Times.Exactly(pageReads));
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable(Times.Exactly(pageReads));
        MockStoreManager
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(pageReads));
    }

    // The store returns the given children of a team for the page that starts at the given cursor, asked for with the
    // walker's page size.
    private void SetupChildPage(
        string teamId,
        string? cursor,
        string[] childIds,
        string? nextCursor
    ) =>
        MockTeamStore
            .Setup(x =>
                x.GetChildPageAsync(
                    teamId,
                    It.Is<PageRequest>(page =>
                        page.Cursor == cursor && page.Limit == DefaultTeamDescendantWalker.PageSize
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTeam>
                {
                    Items = [.. childIds.Select(CreateTeam)],
                    NextCursor = nextCursor,
                }
            )
            .Verifiable();

    private static PersistedTeam CreateTeam(string teamId) =>
        new PersistedTeamBuilder { TeamId = teamId }.Build();
}
