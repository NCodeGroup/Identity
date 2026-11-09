// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates the native EF [ConcurrencyCheck] the stores rely on after ADR-1012. Uses SQLite because the
// EF InMemory provider does not enforce concurrency tokens in the UPDATE ... WHERE clause.
public sealed class GrantStoreConcurrencyTests : IAsyncLifetime, IDisposable
{
    private const string GrantType = "authorization_code";
    private const string HashedKey = "hashed-key-1";
    private const string TenantId = "tenant-1";

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }

    public GrantStoreConcurrencyTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));
        Provider = serviceCollection.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await context.Database.EnsureCreatedAsync();
        context.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");

        var tenantStore = new TenantStore(
            Mock.Of<IStoreProvider>(),
            Provider.GetRequiredService<IIdGenerator<long>>(),
            context
        );
        await tenantStore.AddAsync(CreateTenant(), CancellationToken.None);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        Provider.Dispose();
        Connection.Dispose();
    }

    private GrantStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), Provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static PersistedTenant CreateTenant() =>
        new PersistedTenantBuilder { TenantId = TenantId, DisplayName = "Tenant One" }.Build();

    private static PersistedGrant CreateGrant() =>
        new()
        {
            GrantType = GrantType,
            HashedKey = HashedKey,
            ConcurrencyToken = string.Empty,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = "subject-1",
            SessionId = null,
            RefreshTokenLineageId = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = null,
            ConsumedWhen = null,
            ProtectedPayload = "protected-payload",
        };

    [Fact]
    public async Task UpdateAsync_WhenRowChangedConcurrently_ThrowsViaNativeConcurrency()
    {
        using (var seedScope = Provider.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var store = CreateStore(context);
            await store.AddAsync(CreateGrant(), CancellationToken.None);
            await context.SaveChangesAsync();
        }

        // First reader loads the grant.
        using var scope1 = Provider.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store1 = CreateStore(context1);
        var grant1 = await store1.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);
        Assert.NotNull(grant1);

        // A concurrent writer updates and commits, bumping the row's token.
        using (var scope2 = Provider.CreateScope())
        {
            var context2 = scope2.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var store2 = CreateStore(context2);
            var grant2 = await store2.GetOrDefaultAsync(
                GrantType,
                HashedKey,
                CancellationToken.None
            );
            grant2!.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(1);
            await store2.UpdateAsync(grant2, CancellationToken.None);
            await context2.SaveChangesAsync();
        }

        // The first reader now writes against the stale token it still holds.
        grant1.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(2);
        await store1.UpdateAsync(grant1, CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await context1.SaveChangesAsync()
        );
    }
}
