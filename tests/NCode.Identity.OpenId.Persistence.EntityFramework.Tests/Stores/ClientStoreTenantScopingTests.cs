// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStoreTenantScopingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string ClientA = "client-a";
    private const string ClientB = "client-b";
    private const string SecretB = "secret-b";

    private ServiceProvider Provider { get; }
    private TestAmbientTenantAccessor AmbientTenantAccessor { get; } = new();
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore Store { get; }

    public ClientStoreTenantScopingTests()
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
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        Store = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    #region Helpers

    private static PersistedSecret CreateSecret(string secretId) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = "encoded-value",
        }.Build();

    private static PersistedClient CreateClient(
        string tenantId,
        string clientId,
        params PersistedSecret[] secrets
    ) =>
        new PersistedClientBuilder
        {
            TenantId = tenantId,
            ClientId = clientId,
            Secrets = secrets,
        }.Build();

    private async ValueTask SeedTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");

        var tenant = new PersistedTenantBuilder { TenantId = tenantId }.Build();

        await TenantStore.AddAsync(tenant, cancellationToken);
    }

    // Seeds two tenants, each owning one client (tenant-b's client also has a secret), then detaches everything so
    // subsequent reads hit the database query where the tenant-scoping global filter applies.
    private async ValueTask SeedAsync(CancellationToken cancellationToken)
    {
        await SeedTenantAsync(TenantA, cancellationToken);
        await SeedTenantAsync(TenantB, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        await Store.AddAsync(CreateClient(TenantA, ClientA), cancellationToken);
        await Store.AddAsync(
            CreateClient(TenantB, ClientB, CreateSecret(SecretB)),
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    #endregion

    [Fact]
    public async Task GetOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync(CancellationToken.None);

        AmbientTenantAccessor.TenantId = TenantA;

        var crossTenant = await Store.GetOrDefaultAsync(ClientB, CancellationToken.None);
        var sameTenant = await Store.GetOrDefaultAsync(ClientA, CancellationToken.None);

        Assert.Null(crossTenant);
        Assert.NotNull(sameTenant);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenAnotherTenantsClientIsTracked_ReturnsNull()
    {
        await SeedAsync(CancellationToken.None);

        AmbientTenantAccessor.TenantId = TenantA;
        var tracked = await Store.GetOrDefaultAsync(ClientA, CancellationToken.None);
        Assert.NotNull(tracked);

        AmbientTenantAccessor.TenantId = TenantB;
        var crossTenant = await Store.GetOrDefaultAsync(ClientA, CancellationToken.None);

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync(CancellationToken.None);

        AmbientTenantAccessor.TenantId = TenantA;

        var crossTenant = await Store.GetSecretOrDefaultAsync(
            ClientB,
            SecretB,
            CancellationToken.None
        );

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenUnscoped_ReturnsAllTenants()
    {
        await SeedAsync(CancellationToken.None);

        var tenantA = await Store.GetOrDefaultAsync(ClientA, CancellationToken.None);
        var tenantB = await Store.GetOrDefaultAsync(ClientB, CancellationToken.None);

        Assert.NotNull(tenantA);
        Assert.NotNull(tenantB);
    }

    [Fact]
    public async Task GetPageAsync_WhenScopedToTenant_ReturnsOnlyThatTenant()
    {
        await SeedAsync(CancellationToken.None);

        AmbientTenantAccessor.TenantId = TenantA;

        var page = await Store.GetPageAsync(new PageRequest { Limit = 10 }, CancellationToken.None);

        Assert.All(page.Items, client => Assert.Equal(TenantA, client.TenantId));
        Assert.Contains(page.Items, client => client.ClientId == ClientA);
        Assert.DoesNotContain(page.Items, client => client.ClientId == ClientB);
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
