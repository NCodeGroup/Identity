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
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates the filtered unique index on TenantEntity.NormalizedDomainName. Uses SQLite because the EF InMemory
// provider does not enforce unique indexes.
public sealed class TenantDomainNameIndexTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public TenantDomainNameIndexTests()
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

    private async Task AddTenantAsync(string tenantId, string? domainName)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        var store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, context);

        await store.AddAsync(CreatePersistedTenant(tenantId, domainName), CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private static PersistedTenant CreatePersistedTenant(string tenantId, string? domainName) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            DomainName = domainName,
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
    public async Task NullDomainName_AllowsMultipleTenants()
    {
        await AddTenantAsync("tenant-1", domainName: null);
        await AddTenantAsync("tenant-2", domainName: null);

        Assert.Equal(2, CountTenants());
    }

    [Fact]
    public async Task DistinctNonNullDomainNames_AreAllowed()
    {
        await AddTenantAsync("tenant-1", "a.test");
        await AddTenantAsync("tenant-2", "b.test");

        Assert.Equal(2, CountTenants());
    }

    [Fact]
    public async Task DuplicateNonNullDomainName_IsRejected()
    {
        await AddTenantAsync("tenant-1", "example.test");

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddTenantAsync("tenant-2", "example.test")
        );
    }
}
