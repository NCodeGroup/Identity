// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStorePagingTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;

    public TenantStorePagingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedTenant CreateTenant(
        string tenantId,
        int index,
        string teamId = "platform"
    ) =>
        new()
        {
            TenantId = tenantId,
            TeamId = teamId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(
                    new Dictionary<string, object> { ["index"] = index }
                ),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private async Task SeedAsync(int count)
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        for (var index = 1; index <= count; index++)
        {
            await _store.AddAsync(CreateTenant($"tenant-{index}", index), CancellationToken.None);
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedTwoTeamsAsync()
    {
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _dbContext.SeedTeam(idGenerator, "team-a");
        _dbContext.SeedTeam(idGenerator, "team-b");
        for (var index = 1; index <= 3; index++)
        {
            await _store.AddAsync(
                CreateTenant($"a-tenant-{index}", index, "team-a"),
                CancellationToken.None
            );
        }
        for (var index = 1; index <= 2; index++)
        {
            await _store.AddAsync(
                CreateTenant($"b-tenant-{index}", index, "team-b"),
                CancellationToken.None
            );
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region GetOrDefaultAsync Tests

    [Fact]
    public async Task GetOrDefaultAsync_WhenTheTrackerHoldsTheTenantWithoutItsTeam_LoadsTheTeam()
    {
        await SeedAsync(1);
        _ = _dbContext
            .Tenants.AsTracking()
            .Single(tenant => tenant.NormalizedTenantId == "tenant-1");

        var tenant = await _store.GetOrDefaultAsync("tenant-1", CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal("platform", tenant.TeamId);
    }

    #endregion

    #region Team Tests

    [Fact]
    public async Task GetPageAsync_WithATeam_ReturnsOnlyItsTenants()
    {
        await SeedTwoTeamsAsync();

        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var page = await _store.GetPageAsync(
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
        await SeedTwoTeamsAsync();

        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var page = await _store.GetSettingsPageAsync(
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
        await SeedTwoTeamsAsync();

        Assert.Equal(3, await _store.CountAsync("team-a", CancellationToken.None));
        Assert.Equal(2, await _store.CountAsync("team-b", CancellationToken.None));
        Assert.Equal(0, await _store.CountAsync("team-c", CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WhenTheTeamIsMissing_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.AddAsync(CreateTenant("orphan", 1, "missing"), CancellationToken.None)
        );

        Assert.Equal("The team with TeamId='missing' does not exist.", exception.Message);
    }

    #endregion

    #region GetSettingsPageAsync Tests

    [Fact]
    public async Task GetSettingsPageAsync_TraversesEveryTenantExactlyOnce()
    {
        await SeedAsync(5);

        var seen = new Dictionary<string, int>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await _store.GetSettingsPageAsync(
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
        await SeedAsync(3);

        var page = await _store.GetSettingsPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    #endregion
}
