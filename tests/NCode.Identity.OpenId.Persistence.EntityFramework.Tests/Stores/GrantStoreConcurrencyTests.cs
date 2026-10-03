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

using System.Text.Json;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates the native EF [ConcurrencyCheck] the stores rely on after ADR-0012. Uses SQLite because the
// EF InMemory provider does not enforce concurrency tokens in the UPDATE ... WHERE clause.
public sealed class GrantStoreConcurrencyTests : IDisposable
{
    private const string GrantType = "authorization_code";
    private const string HashedKey = "hashed-key-1";
    private const string TenantId = "tenant-1";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public GrantStoreConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddSingleton<IdValueGenerator>();
        serviceCollection.AddSingleton<UseIdGeneratorConvention>();
        serviceCollection.AddDbContext<OpenIdDbContext>(options => options.UseSqlite(_connection));
        _provider = serviceCollection.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        context.Database.EnsureCreated();

        var tenantStore = new TenantStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            context
        );
        tenantStore.AddAsync(CreateTenant(), CancellationToken.None).GetAwaiter().GetResult();
        context.SaveChanges();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private GrantStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), _provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedTenant CreateTenant() =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            DomainName = null,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = EmptyObject(),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private static PersistedGrant CreateGrant() =>
        new()
        {
            GrantType = GrantType,
            HashedKey = HashedKey,
            ConcurrencyToken = string.Empty,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = "subject-1",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = null,
            ConsumedWhen = null,
            PayloadJson = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
        };

    [Fact]
    public async Task UpdateAsync_WhenRowChangedConcurrently_ThrowsViaNativeConcurrency()
    {
        using (var seedScope = _provider.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var store = CreateStore(context);
            await store.AddAsync(CreateGrant(), CancellationToken.None);
            await context.SaveChangesAsync();
        }

        // First reader loads the grant.
        using var scope1 = _provider.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store1 = CreateStore(context1);
        var grant1 = await store1.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);
        Assert.NotNull(grant1);

        // A concurrent writer updates and commits, bumping the row's token.
        using (var scope2 = _provider.CreateScope())
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
