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

    private async Task AddAsync(string tenantId, DateOnly day, string meter, long count)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context)
            .AddAsync([Usage(tenantId, day, meter, count)], CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<DataContracts.PersistedTenantUsage>> ListAsync(
        DateOnly firstDay,
        DateOnly lastDay
    )
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context)
            .ListAsync(TenantId, firstDay, lastDay, CancellationToken.None);
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
            .AddAsync([Usage(TenantId, Day, Requests, 2)], CancellationToken.None);
        await CreateStore(second)
            .AddAsync([Usage(TenantId, Day, Requests, 5)], CancellationToken.None);

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
            .AddAsync([Usage(TenantId, Day, Requests, 2)], CancellationToken.None);
        await CreateStore(second)
            .AddAsync([Usage(TenantId, Day, Requests, 5)], CancellationToken.None);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        var entry = Assert.Single(await ListAsync(Day, Day));
        Assert.Equal(2, entry.Count);
    }

    [Fact]
    public async Task AddAsync_WhenABatchSpansDaysAndRepeatsAnEntry_AddsEachToItsDay()
    {
        await AddAsync(TenantId, Day, Requests, 1);

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var store = CreateStore(context);
            await store.AddAsync(
                [
                    Usage(TenantId, Day, Requests, 2),
                    Usage(TenantId, Day.AddDays(1), Requests, 3),
                    Usage(TenantId, Day.AddDays(1), Requests, 4),
                ],
                CancellationToken.None
            );
            await store.AddAsync(
                [Usage(TenantId, Day.AddDays(1), TokenRequests, 5)],
                CancellationToken.None
            );
            await store.AddAsync(
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
    public async Task RemoveBeforeAsync_RemovesOnlyEarlierDaysOfEveryTenantWithoutSaving()
    {
        await AddAsync(TenantId, Day.AddDays(-1), Requests, 1);
        await AddAsync("tenant-2", Day.AddDays(-2), Requests, 1);
        await AddAsync(TenantId, Day, Requests, 2);

        int removed;
        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            removed = await CreateStore(context).RemoveBeforeAsync(Day, CancellationToken.None);
        }

        Assert.Equal(2, removed);
        var entry = Assert.Single(await ListAsync(Day.AddDays(-5), Day));
        Assert.Equal(Day, entry.Day);
    }

    #endregion
}
