// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TeamStoreHierarchyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TeamStore _store;

    public TeamStoreHierarchyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _dbContext.Database.EnsureCreated();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TeamStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    private static PersistedTeam CreateTeam(string teamId, string? parentTeamId = null) =>
        new()
        {
            TeamId = teamId,
            ParentTeamId = parentTeamId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            DisplayName = "Team",
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
        };

    private async Task SaveAndClearAsync()
    {
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task AddTeamAsync(string teamId, string? parentTeamId = null)
    {
        await _store.AddAsync(CreateTeam(teamId, parentTeamId), CancellationToken.None);
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

        var team = await _store.GetOrDefaultAsync("acme-web", CancellationToken.None);

        Assert.NotNull(team);
        Assert.Equal("acme", team.ParentTeamId);
        Assert.Equal(["acme"], team.AncestorTeamIds);
    }

    [Fact]
    public async Task AddAsync_WhenTheParentIsMissing_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.AddAsync(CreateTeam("acme-web", "acme"), CancellationToken.None)
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
            await _store.AddAsync(CreateTeam("team-9", parentTeamId), CancellationToken.None)
        );
    }

    #endregion

    #region GetLineageAsync Tests

    [Fact]
    public async Task GetLineageAsync_ReturnsTheTopLevelTeamFirstAndTheTeamLast()
    {
        await SeedTreeAsync();

        var lineage = await _store.GetLineageAsync("acme-web-eu", CancellationToken.None);

        Assert.Equal(["acme", "acme-web", "acme-web-eu"], lineage.Select(team => team.TeamId));
    }

    [Fact]
    public async Task GetLineageAsync_WhenTheTeamDoesNotExist_ReturnsEmpty()
    {
        await SeedTreeAsync();

        var lineage = await _store.GetLineageAsync("missing-team", CancellationToken.None);

        Assert.Empty(lineage);
    }

    #endregion

    #region CountDescendantsAsync Tests

    [Fact]
    public async Task CountDescendantsAsync_CountsEveryLevel()
    {
        await SeedTreeAsync();

        Assert.Equal(3, await _store.CountDescendantsAsync("acme", CancellationToken.None));
        Assert.Equal(1, await _store.CountDescendantsAsync("acme-web", CancellationToken.None));
        Assert.Equal(0, await _store.CountDescendantsAsync("acme-x", CancellationToken.None));
    }

    #endregion

    #region GetChildPageAsync Tests

    [Fact]
    public async Task GetChildPageAsync_ListsOnlyTheTeamsDirectlyBeneath()
    {
        await SeedTreeAsync();

        var first = await _store.GetChildPageAsync(
            "acme",
            new PageRequest { Limit = 1 },
            CancellationToken.None
        );

        Assert.Single(first.Items);
        Assert.NotNull(first.NextCursor);

        var second = await _store.GetChildPageAsync(
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

        var page = await _store.GetChildPageAsync(
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

        Assert.True(await _store.HasDependentsAsync("acme-web", CancellationToken.None));
        Assert.False(await _store.HasDependentsAsync("acme-x", CancellationToken.None));
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenTheTeamHoldsATeam_Throws()
    {
        await SeedTreeAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync("acme-web", CancellationToken.None)
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
        var team = await _store.GetOrDefaultAsync("acme-web", CancellationToken.None);
        Assert.NotNull(team);

        var tokens = await _store.GetConcurrencyTokensAsync(["acme-web"], CancellationToken.None);

        Assert.Single(tokens);
        Assert.Equal(team.ConcurrencyToken, tokens["acme-web"]);
    }

    #endregion
}
