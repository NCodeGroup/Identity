// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// A tenant's secrets are tenant-scoped like every other tenant-owned row: the tenant entity itself stays unscoped
// (so it can be discovered by id during resolution), but its secrets are isolated by the ambient-tenant filter. In
// practice tenant resolution and tenant management read secrets with no ambient scope; these tests assert the filter.
public sealed class TenantStoreTenantScopingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string SecretB = "secret-b";

    private ServiceProvider Provider { get; }
    private TestAmbientTenantAccessor AmbientTenantAccessor { get; } = new();
    private OpenIdDbContext DbContext { get; }
    private TenantStore Store { get; }

    public TenantStoreTenantScopingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddSingleton<IdValueGenerator>();
        serviceCollection.AddSingleton<UseIdGeneratorConvention>();

        Provider = serviceCollection.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<OpenIdDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .UseApplicationServiceProvider(Provider)
            .Options;

        DbContext = new OpenIdDbContext(
            options,
            AmbientTenantAccessor,
            [new CoreModelContributor()]
        );
        Store = new TenantStore(
            Mock.Of<IStoreProvider>(),
            Provider.GetRequiredService<IIdGenerator<long>>(),
            DbContext
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedSecret CreateSecret(string secretId) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = "encoded-value",
        }.Build();

    private async Task SeedTenantAsync(string tenantId, params PersistedSecret[] secrets)
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");

        var tenant = new PersistedTenantBuilder { TenantId = tenantId, Secrets = secrets }.Build();

        await Store.AddAsync(tenant, CancellationToken.None);
    }

    private async Task SeedAsync()
    {
        await SeedTenantAsync(TenantA);
        await SeedTenantAsync(TenantB, CreateSecret(SecretB));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync();

        AmbientTenantAccessor.TenantId = TenantA;

        var crossTenant = await Store.GetSecretOrDefaultAsync(
            TenantB,
            SecretB,
            CancellationToken.None
        );

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenUnscoped_ReturnsSecret()
    {
        await SeedAsync();

        var secret = await Store.GetSecretOrDefaultAsync(TenantB, SecretB, CancellationToken.None);

        Assert.NotNull(secret);
        Assert.Equal(SecretB, secret.SecretId);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenScopedToOtherTenant_LoadsTenantButFiltersItsSecrets()
    {
        await SeedAsync();

        AmbientTenantAccessor.TenantId = TenantA;

        // The tenant entity is unscoped (discoverable by id), but its secrets are tenant-scoped and filtered out.
        var tenantB = await Store.GetOrDefaultAsync(TenantB, CancellationToken.None);

        Assert.NotNull(tenantB);
        Assert.Empty(tenantB.Secrets.Value);
    }

    private sealed class TestAmbientTenantAccessor : IAmbientTenantAccessor
    {
        public string? TenantId { get; set; }

        public bool IsScoped => TenantId is not null;

        public IDisposable BeginScope(string tenantId)
        {
            TenantId = tenantId;
            return new NoopScope();
        }

        private sealed class NoopScope : IDisposable
        {
            public void Dispose() { }
        }
    }
}
