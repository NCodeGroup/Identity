// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TeamStoreHierarchyTests : IDisposable
{
    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TeamStore Store { get; }

    public TeamStoreHierarchyTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        DbContext.Database.EnsureCreated();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TeamStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
        Connection.Dispose();
    }

    private static PersistedTeam CreateTeam(string teamId, string? parentTeamId = null) =>
        new PersistedTeamBuilder
        {
            TeamId = teamId,
            ParentTeamId = parentTeamId,
            DisplayName = "Team",
        }.Build();

    private async Task SaveAndClearAsync()
    {
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task AddTeamAsync(string teamId, string? parentTeamId = null)
    {
        await Store.AddAsync(CreateTeam(teamId, parentTeamId), CancellationToken.None);
        await SaveAndClearAsync();
    }

    private async Task SeedTreeAsync()
    {
        await AddTeamAsync("acme");
        await AddTeamAsync("acme-web", "acme");
        await AddTeamAsync("acme-web-eu", "acme-web");
        await AddTeamAsync("acme-api", "acme");
        await AddTeamAsync("acme-x");
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithAParent_StoresTheTeamBeneathIt()
    {
        await AddTeamAsync("acme");
        await AddTeamAsync("acme-web", "acme");

        var team = await Store.GetOrDefaultAsync("acme-web", CancellationToken.None);

        Assert.NotNull(team);
        Assert.Equal("acme", team.ParentTeamId);
        Assert.Equal(["acme"], team.AncestorTeamIds);
    }

    [Fact]
    public async Task AddAsync_WhenTheParentIsMissing_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.AddAsync(CreateTeam("acme-web", "acme"), CancellationToken.None)
        );
    }

    [Fact]
    public async Task AddAsync_BeyondTheMaximumDepth_Throws()
    {
        string? parentTeamId = null;
        for (var depth = 1; depth <= TeamPaths.MaxDepth; ++depth)
        {
            var teamId = $"team-{depth}";
            await AddTeamAsync(teamId, parentTeamId);
            parentTeamId = teamId;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.AddAsync(CreateTeam("team-9", parentTeamId), CancellationToken.None)
        );
    }

    #endregion

    #region GetLineageAsync Tests

    [Fact]
    public async Task GetLineageAsync_ReturnsTheTopLevelTeamFirstAndTheTeamLast()
    {
        await SeedTreeAsync();

        var lineage = await Store.GetLineageAsync("acme-web-eu", CancellationToken.None);

        Assert.Equal(["acme", "acme-web", "acme-web-eu"], lineage.Select(team => team.TeamId));
    }

    [Fact]
    public async Task GetLineageAsync_WhenTheTeamDoesNotExist_ReturnsEmpty()
    {
        await SeedTreeAsync();

        var lineage = await Store.GetLineageAsync("missing-team", CancellationToken.None);

        Assert.Empty(lineage);
    }

    #endregion

    #region CountDescendantsAsync Tests

    [Fact]
    public async Task CountDescendantsAsync_CountsEveryLevel()
    {
        await SeedTreeAsync();

        Assert.Equal(3, await Store.CountDescendantsAsync("acme", CancellationToken.None));
        Assert.Equal(1, await Store.CountDescendantsAsync("acme-web", CancellationToken.None));
        Assert.Equal(0, await Store.CountDescendantsAsync("acme-x", CancellationToken.None));
    }

    #endregion

    #region GetChildPageAsync Tests

    [Fact]
    public async Task GetChildPageAsync_ListsOnlyTheTeamsDirectlyBeneath()
    {
        await SeedTreeAsync();

        var first = await Store.GetChildPageAsync(
            "acme",
            new PageRequest { Limit = 1 },
            CancellationToken.None
        );

        Assert.Single(first.Items);
        Assert.NotNull(first.NextCursor);

        var second = await Store.GetChildPageAsync(
            "acme",
            new PageRequest { Cursor = first.NextCursor, Limit = 1 },
            CancellationToken.None
        );

        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        Assert.Equal(
            ["acme-api", "acme-web"],
            first.Items.Concat(second.Items).Select(team => team.TeamId).Order()
        );
    }

    [Fact]
    public async Task GetChildPageAsync_WhenTheTeamDoesNotExist_ReturnsAnEmptyPage()
    {
        await SeedTreeAsync();

        var page = await Store.GetChildPageAsync(
            "missing-team",
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    #endregion

    #region HasDependentsAsync Tests

    [Fact]
    public async Task HasDependentsAsync_WhenTheTeamHoldsATeam_ReturnsTrue()
    {
        await SeedTreeAsync();

        Assert.True(await Store.HasDependentsAsync("acme-web", CancellationToken.None));
        Assert.False(await Store.HasDependentsAsync("acme-x", CancellationToken.None));
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenTheTeamHoldsATeam_Throws()
    {
        await SeedTreeAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.RemoveAsync("acme-web", CancellationToken.None)
        );

        Assert.Contains(
            "still has one or more tenants, teams or machine clients",
            exception.Message
        );
    }

    #endregion

    #region GetConcurrencyTokensAsync Tests

    [Fact]
    public async Task GetConcurrencyTokensAsync_WithAChildTeam_ReturnsItsToken()
    {
        await SeedTreeAsync();
        var team = await Store.GetOrDefaultAsync("acme-web", CancellationToken.None);
        Assert.NotNull(team);

        var tokens = await Store.GetConcurrencyTokensAsync(["acme-web"], CancellationToken.None);

        Assert.Single(tokens);
        Assert.Equal(team.ConcurrencyToken, tokens["acme-web"]);
    }

    #endregion
}
