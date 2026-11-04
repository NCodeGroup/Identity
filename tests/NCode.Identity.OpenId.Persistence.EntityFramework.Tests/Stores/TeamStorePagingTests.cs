// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TeamStorePagingTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TeamStore _store;

    public TeamStorePagingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TeamStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedTeam CreateTeam(string teamId) =>
        new()
        {
            TeamId = teamId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            DisplayName = teamId,
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
        };

    private async Task SeedAsync(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            await _store.AddAsync(CreateTeam($"team-{index}"), CancellationToken.None);
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetPageAsync_TraversesEveryTeamExactlyOnceInIdOrder()
    {
        await SeedAsync(5);

        var seen = new List<string>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await _store.GetPageAsync(
                new PageRequest { Cursor = cursor, Limit = 2 },
                CancellationToken.None
            );
            Assert.True(page.Items.Count <= 2);
            seen.AddRange(page.Items.Select(team => team.TeamId));
            cursor = page.NextCursor;
            pageCount++;
        } while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(["team-1", "team-2", "team-3", "team-4", "team-5"], seen);
    }
}
