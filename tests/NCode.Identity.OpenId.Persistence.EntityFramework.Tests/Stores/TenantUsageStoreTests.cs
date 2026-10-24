#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses SQLite because the EF InMemory provider does not enforce concurrency tokens or unique indexes, which keep two
// replicas from losing each other's usage (ADR-0199).
public sealed class TenantUsageStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Requests = "requests";
    private const string TokenRequests = "token_requests";

    private static readonly DateOnly Day = new(2026, 10, 21);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public TenantUsageStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));
        _provider = serviceCollection.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<OpenIdDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private TenantUsageStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), _provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static DataContracts.PersistedTenantUsage Usage(
        string tenantId,
        DateOnly day,
        string meter,
        long count
    ) =>
        new()
        {
            TenantId = tenantId,
            Day = day,
            Meter = meter,
            Count = count,
        };

    private async Task AddBatchAsync(
        Guid replicaId,
        long batchNumber,
        string tenantId,
        DateOnly day,
        string meter,
        long count
    )
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context)
            .AddAsync(
                replicaId,
                batchNumber,
                [Usage(tenantId, day, meter, count)],
                CancellationToken.None
            );
        await context.SaveChangesAsync();
    }

    private Task AddAsync(string tenantId, DateOnly day, string meter, long count) =>
        AddBatchAsync(Guid.NewGuid(), 1, tenantId, day, meter, count);

    private async Task<IReadOnlyList<DataContracts.PersistedTenantUsage>> ListAsync(
        DateOnly firstDay,
        DateOnly lastDay,
        string tenantId = TenantId
    )
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context)
            .ListAsync(tenantId, firstDay, lastDay, CancellationToken.None);
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WhenTheDayHasAnEntry_AddsToIt()
    {
        await AddAsync(TenantId, Day, Requests, 3);
        await AddAsync(TenantId, Day, Requests, 4);

        var usage = await ListAsync(Day, Day);

        var entry = Assert.Single(usage);
        Assert.Equal(7, entry.Count);
        Assert.Equal(Requests, entry.Meter);
        Assert.Equal(TenantId, entry.TenantId);
    }

    [Fact]
    public async Task AddAsync_WhenAnotherWriterUpdatedTheEntryFirst_FailsTheCommit()
    {
        await AddAsync(TenantId, Day, Requests, 1);

        using var firstScope = _provider.CreateScope();
        using var secondScope = _provider.CreateScope();
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

        var entry = Assert.Single(await ListAsync(Day, Day));
        Assert.Equal(3, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenAnotherWriterCreatedTheEntryFirst_FailsTheCommit()
    {
        using var firstScope = _provider.CreateScope();
        using var secondScope = _provider.CreateScope();
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

        var entry = Assert.Single(await ListAsync(Day, Day));
        Assert.Equal(2, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenABatchSpansDaysAndRepeatsAnEntry_AddsEachToItsDay()
    {
        var replicaId = Guid.NewGuid();
        await AddAsync(TenantId, Day, Requests, 1);

        using (var scope = _provider.CreateScope())
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
            (await ListAsync(Day, Day.AddDays(1))).Select(entry =>
                (entry.Day, entry.Meter, entry.Count)
            )
        );
    }

    [Fact]
    public async Task AddAsync_WhenTheReplicaAddedTheBatchBefore_AddsNothing()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3);
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3);

        var entry = Assert.Single(await ListAsync(Day, Day));
        Assert.Equal(3, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenTheBatchFollowsTheReplicasLastBatch_AddsIt()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3);
        await AddBatchAsync(replicaId, 2, TenantId, Day, Requests, 4);

        var entry = Assert.Single(await ListAsync(Day, Day));
        Assert.Equal(7, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenTheBatchIsNumberedBelowTheReplicasLastBatch_AddsNothing()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 2, TenantId, Day, Requests, 4);
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 3);

        var entry = Assert.Single(await ListAsync(Day, Day));
        Assert.Equal(4, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenAnotherUnitOfWorkAddedTheBatchFirst_FailsTheCommit()
    {
        var replicaId = Guid.NewGuid();
        await AddBatchAsync(replicaId, 1, TenantId, Day, Requests, 1);

        using var firstScope = _provider.CreateScope();
        using var secondScope = _provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(first)
            .AddAsync(replicaId, 2, [Usage("tenant-2", Day, Requests, 2)], CancellationToken.None);
        await CreateStore(second)
            .AddAsync(replicaId, 2, [Usage("tenant-3", Day, Requests, 5)], CancellationToken.None);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        Assert.Empty(await ListAsync(Day, Day, "tenant-3"));
    }

    [Fact]
    public async Task AddAsync_WhenTheUsageIsEmpty_Throws()
    {
        using var scope = _provider.CreateScope();
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
        await AddAsync(TenantId, Day.AddDays(1), TokenRequests, 1);
        await AddAsync(TenantId, Day.AddDays(1), Requests, 2);
        await AddAsync(TenantId, Day, Requests, 3);
        await AddAsync(TenantId, Day.AddDays(2), Requests, 4);
        await AddAsync("tenant-2", Day, Requests, 5);

        var usage = await ListAsync(Day, Day.AddDays(1));

        Assert.Equal(
            [
                (Day, Requests, 3L),
                (Day.AddDays(1), Requests, 2L),
                (Day.AddDays(1), TokenRequests, 1L),
            ],
            usage.Select(entry => (entry.Day, entry.Meter, entry.Count))
        );
    }

    #endregion

    #region RemoveBeforeAsync Tests

    [Fact]
    public async Task RemoveBeforeAsync_WhenSaved_RemovesOnlyEarlierDaysOfEveryTenant()
    {
        await AddAsync(TenantId, Day.AddDays(-1), Requests, 1);
        await AddAsync("tenant-2", Day.AddDays(-2), Requests, 1);
        await AddAsync(TenantId, Day, Requests, 2);

        int removed;
        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            removed = await CreateStore(context).RemoveBeforeAsync(Day, 10, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        Assert.Equal(2, removed);
        var entry = Assert.Single(await ListAsync(Day.AddDays(-5), Day));
        Assert.Equal(Day, entry.Day);
    }

    [Fact]
    public async Task RemoveBeforeAsync_WhenMoreEntriesThanTheLimit_RemovesTheLimit()
    {
        await AddAsync(TenantId, Day.AddDays(-1), Requests, 1);
        await AddAsync(TenantId, Day.AddDays(-1), TokenRequests, 1);
        await AddAsync(TenantId, Day.AddDays(-2), Requests, 1);

        int removed;
        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            removed = await CreateStore(context).RemoveBeforeAsync(Day, 2, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        Assert.Equal(2, removed);
        Assert.Single(await ListAsync(Day.AddDays(-5), Day));
    }

    [Fact]
    public async Task RemoveBeforeAsync_WhenTheLimitIsBelowOne_Throws()
    {
        using var scope = _provider.CreateScope();
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
        await AddBatchAsync(earlier, 1, TenantId, Day.AddDays(-1), Requests, 1);
        await AddBatchAsync(current, 1, TenantId, Day, Requests, 2);

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).RemoveBeforeAsync(Day, 10, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        using var readScope = _provider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var replicaIds = await readContext
            .Set<TenantUsageCheckpointEntity>()
            .Select(entity => entity.ReplicaId)
            .ToListAsync();

        Assert.Equal([current], replicaIds);
    }

    #endregion
}
