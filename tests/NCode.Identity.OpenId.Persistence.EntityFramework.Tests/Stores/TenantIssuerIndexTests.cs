// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }

    public TenantIssuerIndexTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));
        Provider = serviceCollection.BuildServiceProvider();

        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Provider.Dispose();
        Connection.Dispose();
    }

    #region Helpers

    private async ValueTask AddTenantAsync(
        string tenantId,
        string? issuer,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        var store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, context);

        context.SeedTeam(idGenerator, "platform");
        await store.AddAsync(CreatePersistedTenant(tenantId, issuer), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static PersistedTenant CreatePersistedTenant(string tenantId, string? issuer) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            Issuer = issuer,
            DisplayName = "Tenant",
        }.Build();

    private int CountTenants()
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return context.Tenants.Count();
    }

    #endregion

    [Fact]
    public async Task NullIssuer_AllowsMultipleTenants()
    {
        await AddTenantAsync("tenant-1", issuer: null, CancellationToken.None);
        await AddTenantAsync("tenant-2", issuer: null, CancellationToken.None);

        Assert.Equal(2, CountTenants());
    }

    [Fact]
    public async Task DistinctIssuers_AreAllowed()
    {
        await AddTenantAsync("tenant-1", "https://a.test", CancellationToken.None);
        await AddTenantAsync("tenant-2", "https://b.test", CancellationToken.None);

        Assert.Equal(2, CountTenants());
    }

    [Fact]
    public async Task DuplicateIssuer_IsRejected()
    {
        await AddTenantAsync("tenant-1", "https://example.test", CancellationToken.None);

        // An issuer IS the tenant's identity and is asserted in every token it mints, so two tenants claiming one is a
        // protocol-level collision the schema must refuse.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddTenantAsync("tenant-2", "https://example.test", CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task SharedIssuerHost_IsAllowed()
    {
        // Path-based tenants legitimately share a host, which is why the derived host index must NOT be unique. Making
        // it unique would make the path strategy unusable beyond a single tenant.
        await AddTenantAsync("tenant-1", "https://op.test/tenant-a", CancellationToken.None);
        await AddTenantAsync("tenant-2", "https://op.test/tenant-b", CancellationToken.None);

        Assert.Equal(2, CountTenants());
    }
}
