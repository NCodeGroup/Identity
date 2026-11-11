// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TeamStorePagingTests : IDisposable
{
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TeamStore Store { get; }

    public TeamStorePagingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TeamStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedTeam CreateTeam(string teamId) =>
        new PersistedTeamBuilder { TeamId = teamId }.Build();

    private async ValueTask SeedAsync(int count, CancellationToken cancellationToken)
    {
        for (var index = 1; index <= count; index++)
        {
            await Store.AddAsync(CreateTeam($"team-{index}"), cancellationToken);
        }
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetPageAsync_TraversesEveryTeamExactlyOnceInIdOrder()
    {
        await SeedAsync(5, CancellationToken.None);

        var seen = new List<string>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await Store.GetPageAsync(
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
