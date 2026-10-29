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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Adds through SQLite, because the EF InMemory provider does not enforce the unique event identifier or the foreign key
// that ties a resource to its record, and removes through the InMemory provider, because EF's SQLite provider cannot
// compare a DateTimeOffset in a query; the host's Npgsql provider does both (ADR-0303).
public sealed class AuditRecordStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public AuditRecordStoreTests()
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

    private AuditRecordStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), _provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static PersistedAuditRecord Record(
        Guid? eventId = null,
        DateTimeOffset? eventTime = null,
        params PersistedAuditResource[] resources
    ) =>
        new()
        {
            EventId = eventId ?? Guid.CreateVersion7(),
            EventTime = eventTime ?? Now,
            EventName = "client.updated",
            EventCategory = "management",
            Outcome = "success",
            Severity = "info",
            TenantId = "tenant-1",
            SubjectId = "user-1",
            ClientId = "client-1",
            ActorType = "user",
            ActorId = "admin-1",
            TraceId = "0af7651916cd43dd8448eb211c80319c",
            SourceIp = "203.0.113.7",
            Resources = resources,
            Record = """{"schema_version":"1.1"}""",
        };

    private static ServiceProvider CreateInMemoryProvider()
    {
        // The configuration runs for each context, so every context of the provider must name the same database.
        var databaseName = Guid.NewGuid().ToString("N");
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        return serviceCollection.BuildServiceProvider();
    }

    private Task<int> AddAsync(params PersistedAuditRecord[] records) =>
        AddAsync(_provider, records);

    private async Task<int> AddAsync(
        ServiceProvider provider,
        params PersistedAuditRecord[] records
    )
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var added = await CreateStore(context).AddAsync(records, CancellationToken.None);
        await context.SaveChangesAsync();
        return added;
    }

    private async Task<int> RemoveBeforeAsync(
        ServiceProvider provider,
        DateTimeOffset time,
        int limit
    )
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var removed = await CreateStore(context)
            .RemoveBeforeAsync(time, limit, CancellationToken.None);
        await context.SaveChangesAsync();
        return removed;
    }

    private Task<(int Records, int Resources)> CountAsync() => CountAsync(_provider);

    private static async Task<(int Records, int Resources)> CountAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var records = await context.AuditRecords.CountAsync();
        var resources = await context.AuditRecordResources.CountAsync();
        return (records, resources);
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithAResourcefulRecord_KeepsItsEnvelopeAndResourcesInOrder()
    {
        var record = Record(
            resources:
            [
                new PersistedAuditResource { Type = "client", Id = "client-1" },
                new PersistedAuditResource { Type = "tenant", Id = "tenant-1" },
            ]
        );

        var added = await AddAsync(record);

        Assert.Equal(1, added);
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var entity = await context
            .AuditRecords.Include(e => e.Resources)
            .SingleAsync(e => e.EventId == record.EventId);
        Assert.Equal(Now, entity.EventTime);
        Assert.Equal("client.updated", entity.EventName);
        Assert.Equal("tenant-1", entity.TenantId);
        Assert.Equal("admin-1", entity.ActorId);
        Assert.Equal("203.0.113.7", entity.SourceIp);
        Assert.Equal(record.Record, entity.Record);
        Assert.Equal(
            [("client", "client-1", 0), ("tenant", "tenant-1", 1)],
            entity.Resources.OrderBy(r => r.Ordinal).Select(r => (r.Type, r.ResourceId!, r.Ordinal))
        );
    }

    [Fact]
    public async Task AddAsync_WhenTheStoreHoldsAnEvent_AddsOnlyTheOthers()
    {
        var held = Record();
        await AddAsync(held);

        var added = await AddAsync(held, Record());

        Assert.Equal(1, added);
        Assert.Equal((2, 0), await CountAsync());
    }

    [Fact]
    public async Task AddAsync_WhenABatchHoldsAnEventTwice_AddsItOnce()
    {
        var eventId = Guid.CreateVersion7();

        var added = await AddAsync(Record(eventId), Record(eventId));

        Assert.Equal(1, added);
        Assert.Equal((1, 0), await CountAsync());
    }

    [Fact]
    public async Task AddAsync_WhenEmpty_Throws()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await CreateStore(context).AddAsync([], CancellationToken.None)
        );
    }

    #endregion

    #region RemoveBeforeAsync Tests

    [Fact]
    public async Task RemoveBeforeAsync_RemovesTheRecordsPastTheCutoffWithTheirResources()
    {
        await using var provider = CreateInMemoryProvider();
        var resource = new PersistedAuditResource { Type = "client", Id = "client-1" };
        await AddAsync(
            provider,
            Record(eventTime: Now.AddDays(-91), resources: [resource]),
            Record(eventTime: Now.AddDays(-90).AddSeconds(-1), resources: [resource]),
            Record(eventTime: Now.AddDays(-89), resources: [resource])
        );

        var removed = await RemoveBeforeAsync(provider, Now.AddDays(-90), limit: 10);

        Assert.Equal(2, removed);
        Assert.Equal((1, 1), await CountAsync(provider));
    }

    [Fact]
    public async Task RemoveBeforeAsync_RemovesAtMostTheLimit()
    {
        await using var provider = CreateInMemoryProvider();
        await AddAsync(
            provider,
            Record(eventTime: Now.AddDays(-100)),
            Record(eventTime: Now.AddDays(-100)),
            Record(eventTime: Now.AddDays(-100))
        );

        var first = await RemoveBeforeAsync(provider, Now, limit: 2);
        var second = await RemoveBeforeAsync(provider, Now, limit: 2);

        Assert.Equal(2, first);
        Assert.Equal(1, second);
        Assert.Equal((0, 0), await CountAsync(provider));
    }

    [Fact]
    public async Task RemoveBeforeAsync_WhenTheLimitIsBelowOne_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await RemoveBeforeAsync(_provider, Now, limit: 0)
        );
    }

    #endregion
}
