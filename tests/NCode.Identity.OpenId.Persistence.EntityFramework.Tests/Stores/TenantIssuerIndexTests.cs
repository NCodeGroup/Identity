#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates the filtered UNIQUE index on TenantEntity.NormalizedIssuer, and that the derived NormalizedIssuerHost
// index is deliberately NOT unique. Uses SQLite because the EF InMemory provider does not enforce unique indexes.
public sealed class TenantIssuerIndexTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public TenantIssuerIndexTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));
        _provider = serviceCollection.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    #region Helpers

    private async Task AddTenantAsync(string tenantId, string? issuer)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        var store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, context);

        await store.AddAsync(CreatePersistedTenant(tenantId, issuer), CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private static PersistedTenant CreatePersistedTenant(string tenantId, string? issuer) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            Issuer = issuer,
            IsDisabled = false,
            DisplayName = "Tenant",
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private int CountTenants()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return context.Tenants.Count();
    }

    #endregion

    [Fact]
    public async Task NullIssuer_AllowsMultipleTenants()
    {
        await AddTenantAsync("tenant-1", issuer: null);
        await AddTenantAsync("tenant-2", issuer: null);

        Assert.Equal(2, CountTenants());
    }

    [Fact]
    public async Task DistinctIssuers_AreAllowed()
    {
        await AddTenantAsync("tenant-1", "https://a.test");
        await AddTenantAsync("tenant-2", "https://b.test");

        Assert.Equal(2, CountTenants());
    }

    [Fact]
    public async Task DuplicateIssuer_IsRejected()
    {
        await AddTenantAsync("tenant-1", "https://example.test");

        // An issuer IS the tenant's identity and is asserted in every token it mints, so two tenants claiming one is a
        // protocol-level collision the schema must refuse.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddTenantAsync("tenant-2", "https://example.test")
        );
    }

    [Fact]
    public async Task SharedIssuerHost_IsAllowed()
    {
        // Path-based tenants legitimately share a host, which is why the derived host index must NOT be unique. Making
        // it unique would make the path strategy unusable beyond a single tenant.
        await AddTenantAsync("tenant-1", "https://op.test/tenant-a");
        await AddTenantAsync("tenant-2", "https://op.test/tenant-b");

        Assert.Equal(2, CountTenants());
    }
}
