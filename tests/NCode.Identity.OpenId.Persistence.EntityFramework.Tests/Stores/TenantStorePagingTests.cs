// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStorePagingTests : IDisposable
{
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore Store { get; }

    public TenantStorePagingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedTenant CreateTenant(
        string tenantId,
        int index,
        string teamId = "platform"
    ) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = teamId,
            Settings = JsonSerializer.SerializeToElement(
                new Dictionary<string, object> { ["index"] = index }
            ),
        }.Build();

    private async ValueTask SeedAsync(int count, CancellationToken cancellationToken)
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        for (var index = 1; index <= count; index++)
        {
            await Store.AddAsync(CreateTenant($"tenant-{index}", index), cancellationToken);
        }
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask SeedTwoTeamsAsync(CancellationToken cancellationToken)
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        DbContext.SeedTeam(idGenerator, "team-a");
        DbContext.SeedTeam(idGenerator, "team-b");
        for (var index = 1; index <= 3; index++)
        {
            await Store.AddAsync(
                CreateTenant($"a-tenant-{index}", index, "team-a"),
                cancellationToken
            );
        }
        for (var index = 1; index <= 2; index++)
        {
            await Store.AddAsync(
                CreateTenant($"b-tenant-{index}", index, "team-b"),
                cancellationToken
            );
        }
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    #region GetOrDefaultAsync Tests

    [Fact]
    public async Task GetOrDefaultAsync_WhenTheTrackerHoldsTheTenantWithoutItsTeam_LoadsTheTeam()
    {
        await SeedAsync(1, CancellationToken.None);
        _ = DbContext
            .Tenants.AsTracking()
            .Single(tenant => tenant.NormalizedTenantId == "tenant-1");

        var tenant = await Store.GetOrDefaultAsync("tenant-1", CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal("platform", tenant.TeamId);
    }

    #endregion

    #region Team Tests

    [Fact]
    public async Task GetPageAsync_WithATeam_ReturnsOnlyItsTenants()
    {
        await SeedTwoTeamsAsync(CancellationToken.None);

        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var page = await Store.GetPageAsync(
                "TEAM-A",
                new PageRequest { Cursor = cursor, Limit = 2 },
                CancellationToken.None
            );
            foreach (var tenant in page.Items)
            {
                Assert.Equal("team-a", tenant.TeamId);
                seen.Add(tenant.TenantId);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        Assert.Equal(["a-tenant-1", "a-tenant-2", "a-tenant-3"], seen);
    }

    [Fact]
    public async Task GetSettingsPageAsync_WithATeam_TraversesOnlyItsTenants()
    {
        await SeedTwoTeamsAsync(CancellationToken.None);

        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var page = await Store.GetSettingsPageAsync(
                "team-b",
                new PageRequest { Cursor = cursor, Limit = 1 },
                CancellationToken.None
            );
            seen.AddRange(page.Items.Select(settings => settings.TenantId));
            cursor = page.NextCursor;
        } while (cursor is not null);

        Assert.Equal(["b-tenant-1", "b-tenant-2"], seen);
    }

    [Fact]
    public async Task CountAsync_CountsTheTeamsTenants()
    {
        await SeedTwoTeamsAsync(CancellationToken.None);

        Assert.Equal(3, await Store.CountAsync("team-a", CancellationToken.None));
        Assert.Equal(2, await Store.CountAsync("team-b", CancellationToken.None));
        Assert.Equal(0, await Store.CountAsync("team-c", CancellationToken.None));
    }

    [Fact]
    public async Task CountAsync_CountsTheTenantsOfEveryTeamBeneath()
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        var parent = DbContext.SeedTeam(idGenerator, "team-a");
        DbContext.SeedChildTeam(idGenerator, parent, "team-a-web");
        await Store.AddAsync(CreateTenant("a-tenant-1", 1, "team-a"), CancellationToken.None);
        for (var index = 1; index <= 2; index++)
        {
            await Store.AddAsync(
                CreateTenant($"web-tenant-{index}", index, "team-a-web"),
                CancellationToken.None
            );
        }
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(3, await Store.CountAsync("team-a", CancellationToken.None));
        Assert.Equal(2, await Store.CountAsync("team-a-web", CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WhenTheTeamIsMissing_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.AddAsync(CreateTenant("orphan", 1, "missing"), CancellationToken.None)
        );

        Assert.Equal("The team with TeamId='missing' does not exist.", exception.Message);
    }

    #endregion

    #region GetSettingsPageAsync Tests

    [Fact]
    public async Task GetSettingsPageAsync_TraversesEveryTenantExactlyOnce()
    {
        await SeedAsync(5, CancellationToken.None);

        var seen = new Dictionary<string, int>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await Store.GetSettingsPageAsync(
                new PageRequest { Cursor = cursor, Limit = 2 },
                CancellationToken.None
            );
            Assert.True(page.Items.Count <= 2);
            foreach (var settings in page.Items)
            {
                Assert.False(string.IsNullOrEmpty(settings.ConcurrencyToken));
                seen.Add(settings.TenantId, settings.Value.GetProperty("index").GetInt32());
            }
            cursor = page.NextCursor;
            pageCount++;
        } while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(5, seen.Count);
        for (var index = 1; index <= 5; index++)
        {
            Assert.Equal(index, seen[$"tenant-{index}"]);
        }
    }

    [Fact]
    public async Task GetSettingsPageAsync_ReadsWithoutTracking()
    {
        await SeedAsync(3, CancellationToken.None);

        var page = await Store.GetSettingsPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
        Assert.Empty(DbContext.ChangeTracker.Entries());
    }

    #endregion
}
