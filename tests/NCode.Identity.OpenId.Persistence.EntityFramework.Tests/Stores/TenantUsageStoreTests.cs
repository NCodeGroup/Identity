// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses SQLite because the EF InMemory provider does not enforce concurrency tokens or unique indexes, which keep two
// replicas from losing each other's usage (ADR-1199).
public sealed class TenantUsageStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Requests = "requests";
    private const string TokenRequests = "token_requests";

    private static DateOnly Day { get; } = new(2026, 10, 21);

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }

    public TenantUsageStoreTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));
        Provider = serviceCollection.BuildServiceProvider();

        using var scope = Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<OpenIdDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        Provider.Dispose();
        Connection.Dispose();
    }

    private TenantUsageStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), Provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static DataContracts.PersistedTenantUsage Usage(
        string tenantId,
        DateOnly day,
        string meter,
        long count,
        string teamPath = "/platform/"
    ) =>
        new()
        {
            TenantId = tenantId,
            TeamPath = teamPath,
            Day = day,
            Meter = meter,
            Count = count,
        };

    private async ValueTask AddBatchAsync(
        Guid replicaId,
        long batchNumber,
        string tenantId,
        DateOnly day,
        string meter,
        long count,
        string teamPath,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context)
            .AddAsync(
                replicaId,
                batchNumber,
                [Usage(tenantId, day, meter, count, teamPath)],
                cancellationToken
            );
        await context.SaveChangesAsync(cancellationToken);
    }

    private ValueTask AddBatchAsync(
        Guid replicaId,
        long batchNumber,
        string tenantId,
        DateOnly day,
        string meter,
        long count,
        CancellationToken cancellationToken
    ) =>
        AddBatchAsync(
            replicaId,
            batchNumber,
            tenantId,
            day,
            meter,
            count,
            "/platform/",
            cancellationToken
        );

    private ValueTask AddAsync(
        string tenantId,
        DateOnly day,
        string meter,
        long count,
        string teamPath,
        CancellationToken cancellationToken
    ) => AddBatchAsync(Guid.NewGuid(), 1, tenantId, day, meter, count, teamPath, cancellationToken);

    private ValueTask AddAsync(
        string tenantId,
        DateOnly day,
        string meter,
        long count,
        CancellationToken cancellationToken
    ) => AddAsync(tenantId, day, meter, count, "/platform/", cancellationToken);

    private async ValueTask<IReadOnlyList<DataContracts.PersistedTenantUsage>> ListByTeamAsync(
        string teamPath,
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context)
            .ListByTeamAsync(teamPath, firstDay, lastDay, cancellationToken);
    }

    private async ValueTask<IReadOnlyList<DataContracts.PersistedTenantUsage>> ListAsync(
        DateOnly firstDay,
        DateOnly lastDay,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).ListAsync(tenantId, firstDay, lastDay, cancellationToken);
    }

    private ValueTask<IReadOnlyList<DataContracts.PersistedTenantUsage>> ListAsync(
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken
    ) => ListAsync(firstDay, lastDay, TenantId, cancellationToken);

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WhenTheDayHasAnEntry_AddsToIt()
    {
        await AddAsync(TenantId, Day, Requests, 3, CancellationToken.None);
        await AddAsync(TenantId, Day, Requests, 4, CancellationToken.None);

        var usage = await ListAsync(Day, Day, CancellationToken.None);

        var entry = Assert.Single(usage);
        Assert.Equal(7, entry.Count);
        Assert.Equal(Requests, entry.Meter);
        Assert.Equal(TenantId, entry.TenantId);
    }

    [Fact]
    public async Task AddAsync_WhenAnotherWriterUpdatedTheEntryFirst_FailsTheCommit()
    {
        await AddAsync(TenantId, Day, Requests, 1, CancellationToken.None);

        using var firstScope = Provider.CreateScope();
        using var secondScope = Provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(first)
            .AddAsync(
                Guid.NewGuid(),
                1,
                [Usage(TenantId, Day, Requests, 2)],
                CancellationToken.None
            );
        await CreateStore(second)
            .AddAsync(
                Guid.NewGuid(),
                1,
                [Usage(TenantId, Day, Requests, 5)],
                CancellationToken.None
            );

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        var entry = Assert.Single(await ListAsync(Day, Day, CancellationToken.None));
        Assert.Equal(3, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenAnotherWriterCreatedTheEntryFirst_FailsTheCommit()
    {
        using var firstScope = Provider.CreateScope();
        using var secondScope = Provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(first)
            .AddAsync(
                Guid.NewGuid(),
                1,
                [Usage(TenantId, Day, Requests, 2)],
                CancellationToken.None
            );
        await CreateStore(second)
            .AddAsync(
                Guid.NewGuid(),
                1,
                [Usage(TenantId, Day, Requests, 5)],
                CancellationToken.None
            );

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        var entry = Assert.Single(await ListAsync(Day, Day, CancellationToken.None));
        Assert.Equal(2, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenABatchSpansDaysAndRepeatsAnEntry_AddsEachToItsDay()
    {
        var replicaId = Guid.NewGuid();
        await AddAsync(TenantId, Day, Requests, 1, CancellationToken.None);

        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var store = CreateStore(context);
            await store.AddAsync(
                replicaId,
                1,
                [
                    Usage(TenantId, Day, Requests, 2),
                    Usage(TenantId, Day.AddDays(1), Requests, 3),
                    Usage(TenantId, Day.AddDays(1), Requests, 4),
                ],
                CancellationToken.None
            );
            await store.AddAsync(
                replicaId,
                2,
                [Usage(TenantId, Day.AddDays(1), TokenRequests, 5)],
                CancellationToken.None
            );
            await store.AddAsync(
                replicaId,
                3,
                [Usage(TenantId, Day.AddDays(1), TokenRequests, 6)],
                CancellationToken.None
            );
            await context.SaveChangesAsync();
        }

        Assert.Equal(
            [
                (Day, Requests, 3L),
                (Day.AddDays(1), Requests, 7L),
                (Day.AddDays(1), TokenRequests, 11L),
            ],
            (await ListAsync(Day, Day.AddDays(1), CancellationToken.None)).Select(entry =>
                (entry.Day, entry.Meter, entry.Count)
            )
        );
    }

    [Fact]
    public async Task AddAsync_WhenTheReplicaAddedTheBatchBefore_AddsNothing()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3, CancellationToken.None);
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3, CancellationToken.None);

        var entry = Assert.Single(await ListAsync(Day, Day, CancellationToken.None));
        Assert.Equal(3, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenTheBatchFollowsTheReplicasLastBatch_AddsIt()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3, CancellationToken.None);
        await AddBatchAsync(replicaId, 2, TenantId, Day, Requests, 4, CancellationToken.None);

        var entry = Assert.Single(await ListAsync(Day, Day, CancellationToken.None));
        Assert.Equal(7, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenTheBatchIsNumberedBelowTheReplicasLastBatch_AddsNothing()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 2, TenantId, Day, Requests, 4, CancellationToken.None);
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3, CancellationToken.None);

        var entry = Assert.Single(await ListAsync(Day, Day, CancellationToken.None));
        Assert.Equal(4, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenAnotherUnitOfWorkAddedTheBatchFirst_FailsTheCommit()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 1, CancellationToken.None);

        using var firstScope = Provider.CreateScope();
        using var secondScope = Provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(first)
            .AddAsync(replicaId, 2, [Usage("tenant-2", Day, Requests, 2)], CancellationToken.None);
        await CreateStore(second)
            .AddAsync(replicaId, 2, [Usage("tenant-3", Day, Requests, 5)], CancellationToken.None);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        Assert.Empty(await ListAsync(Day, Day, "tenant-3", CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WhenTheUsageIsEmpty_Throws()
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateStore(context).AddAsync(Guid.NewGuid(), 1, [], CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region ListAsync Tests

    [Fact]
    public async Task ListAsync_ReturnsTheTenantsRangeOrderedByDayThenMeter()
    {
        await AddAsync(TenantId, Day.AddDays(1), TokenRequests, 1, CancellationToken.None);
        await AddAsync(TenantId, Day.AddDays(1), Requests, 2, CancellationToken.None);
        await AddAsync(TenantId, Day, Requests, 3, CancellationToken.None);
        await AddAsync(TenantId, Day.AddDays(2), Requests, 4, CancellationToken.None);
        await AddAsync("tenant-2", Day, Requests, 5, CancellationToken.None);

        var usage = await ListAsync(Day, Day.AddDays(1), CancellationToken.None);

        Assert.Equal(
            [
                (Day, Requests, 3L),
                (Day.AddDays(1), Requests, 2L),
                (Day.AddDays(1), TokenRequests, 1L),
            ],
            usage.Select(entry => (entry.Day, entry.Meter, entry.Count))
        );
    }

    [Fact]
    public async Task ListByTeamAsync_ReturnsEveryTenantOfTheTeamInTheRange()
    {
        await AddAsync("tenant-2", Day, Requests, 1, "/team-a/", CancellationToken.None);
        await AddAsync("tenant-1", Day.AddDays(1), Requests, 2, "/team-a/", CancellationToken.None);
        await AddAsync("tenant-1", Day, TokenRequests, 3, "/team-a/", CancellationToken.None);
        await AddAsync("tenant-1", Day, Requests, 4, "/team-a/", CancellationToken.None);
        await AddAsync("tenant-1", Day.AddDays(2), Requests, 5, "/team-a/", CancellationToken.None);
        await AddAsync("tenant-3", Day, Requests, 6, "/team-b/", CancellationToken.None);

        var usage = await ListByTeamAsync("/team-a/", Day, Day.AddDays(1), CancellationToken.None);

        Assert.Equal(
            [
                ("tenant-1", Day, Requests, 4L),
                ("tenant-1", Day, TokenRequests, 3L),
                ("tenant-2", Day, Requests, 1L),
                ("tenant-1", Day.AddDays(1), Requests, 2L),
            ],
            usage.Select(entry => (entry.TenantId, entry.Day, entry.Meter, entry.Count))
        );
        Assert.All(usage, entry => Assert.Equal("/team-a/", entry.TeamPath));
    }

    [Fact]
    public async Task ListByTeamAsync_ListsTheTenantsOfEveryTeamBeneath()
    {
        await AddAsync("tenant-1", Day, Requests, 1, "/team-a/", CancellationToken.None);
        await AddAsync("tenant-2", Day, Requests, 2, "/team-a/team-a-web/", CancellationToken.None);
        await AddAsync("tenant-3", Day, Requests, 3, "/team-a-x/", CancellationToken.None);

        var usage = await ListByTeamAsync("/team-a/", Day, Day, CancellationToken.None);

        Assert.Equal(
            [("tenant-1", "/team-a/"), ("tenant-2", "/team-a/team-a-web/")],
            usage.Select(entry => (entry.TenantId, entry.TeamPath))
        );
    }

    [Fact]
    public async Task ListByTeamAsync_ListsRowsOfATenantWithNoTenantRow()
    {
        // No tenant row exists for the usage row.
        await AddAsync("deleted-tenant", Day, Requests, 7, "/team-a/", CancellationToken.None);

        var entry = Assert.Single(
            await ListByTeamAsync("/team-a/", Day, Day, CancellationToken.None)
        );

        Assert.Equal("deleted-tenant", entry.TenantId);
        Assert.Equal(7, entry.Count);
    }

    #endregion

    #region RemoveBeforeAsync Tests

    [Fact]
    public async Task RemoveBeforeAsync_WhenSaved_RemovesOnlyEarlierDaysOfEveryTenant()
    {
        await AddAsync(TenantId, Day.AddDays(-1), Requests, 1, CancellationToken.None);
        await AddAsync("tenant-2", Day.AddDays(-2), Requests, 1, CancellationToken.None);
        await AddAsync(TenantId, Day, Requests, 2, CancellationToken.None);

        int removed;
        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            removed = await CreateStore(context).RemoveBeforeAsync(Day, 10, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        Assert.Equal(2, removed);
        var entry = Assert.Single(await ListAsync(Day.AddDays(-5), Day, CancellationToken.None));
        Assert.Equal(Day, entry.Day);
    }

    [Fact]
    public async Task RemoveBeforeAsync_WhenMoreEntriesThanTheLimit_RemovesTheLimit()
    {
        await AddAsync(TenantId, Day.AddDays(-1), Requests, 1, CancellationToken.None);
        await AddAsync(TenantId, Day.AddDays(-1), TokenRequests, 1, CancellationToken.None);
        await AddAsync(TenantId, Day.AddDays(-2), Requests, 1, CancellationToken.None);

        int removed;
        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            removed = await CreateStore(context).RemoveBeforeAsync(Day, 2, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        Assert.Equal(2, removed);
        Assert.Single(await ListAsync(Day.AddDays(-5), Day, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveBeforeAsync_WhenTheLimitIsBelowOne_Throws()
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateStore(context).RemoveBeforeAsync(Day, 0, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task RemoveBeforeAsync_RemovesTheCheckpointsOfReplicasThatLastAddedEarlierDays()
    {
        var earlier = Guid.NewGuid();
        var current = Guid.NewGuid();
        await AddBatchAsync(
            earlier,
            1,
            TenantId,
            Day.AddDays(-1),
            Requests,
            1,
            CancellationToken.None
        );
        await AddBatchAsync(current, 1, TenantId, Day, Requests, 2, CancellationToken.None);

        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).RemoveBeforeAsync(Day, 10, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        using var readScope = Provider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var replicaIds = await readContext
            .Set<TenantUsageCheckpointEntity>()
            .Select(entity => entity.ReplicaId)
            .ToListAsync();

        Assert.Equal([current], replicaIds);
    }

    #endregion
}
