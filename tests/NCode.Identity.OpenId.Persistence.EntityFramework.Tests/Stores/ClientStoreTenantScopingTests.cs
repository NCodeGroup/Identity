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

public sealed class ClientStoreTenantScopingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string ClientA = "client-a";
    private const string ClientB = "client-b";
    private const string SecretB = "secret-b";

    private readonly ServiceProvider _provider;
    private readonly TestAmbientTenantAccessor _ambientTenantAccessor = new();
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _store;

    public ClientStoreTenantScopingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddSingleton<IdValueGenerator>();
        serviceCollection.AddSingleton<UseIdGeneratorConvention>();

        _provider = serviceCollection.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<OpenIdDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .UseApplicationServiceProvider(_provider)
            .Options;

        _dbContext = new OpenIdDbContext(
            options,
            _ambientTenantAccessor,
            [new CoreModelContributor()]
        );
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
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

    private async Task SeedTenantAsync(string tenantId)
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");

        var tenant = new PersistedTenantBuilder { TenantId = tenantId }.Build();

        await _tenantStore.AddAsync(tenant, CancellationToken.None);
    }

    // Seeds two tenants, each owning one client (tenant-b's client also has a secret), then detaches everything so
    // subsequent reads hit the database query where the tenant-scoping global filter applies.
    private async Task SeedAsync()
    {
        await SeedTenantAsync(TenantA);
        await SeedTenantAsync(TenantB);
        await _dbContext.SaveChangesAsync();

        await _store.AddAsync(CreateClient(TenantA, ClientA), CancellationToken.None);
        await _store.AddAsync(
            CreateClient(TenantB, ClientB, CreateSecret(SecretB)),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #endregion

    [Fact]
    public async Task GetOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        var crossTenant = await _store.GetOrDefaultAsync(ClientB, CancellationToken.None);
        var sameTenant = await _store.GetOrDefaultAsync(ClientA, CancellationToken.None);

        Assert.Null(crossTenant);
        Assert.NotNull(sameTenant);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenAnotherTenantsClientIsTracked_ReturnsNull()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;
        var tracked = await _store.GetOrDefaultAsync(ClientA, CancellationToken.None);
        Assert.NotNull(tracked);

        _ambientTenantAccessor.TenantId = TenantB;
        var crossTenant = await _store.GetOrDefaultAsync(ClientA, CancellationToken.None);

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        var crossTenant = await _store.GetSecretOrDefaultAsync(
            ClientB,
            SecretB,
            CancellationToken.None
        );

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenUnscoped_ReturnsAllTenants()
    {
        await SeedAsync();

        var tenantA = await _store.GetOrDefaultAsync(ClientA, CancellationToken.None);
        var tenantB = await _store.GetOrDefaultAsync(ClientB, CancellationToken.None);

        Assert.NotNull(tenantA);
        Assert.NotNull(tenantB);
    }

    [Fact]
    public async Task GetPageAsync_WhenScopedToTenant_ReturnsOnlyThatTenant()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        var page = await _store.GetPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

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
