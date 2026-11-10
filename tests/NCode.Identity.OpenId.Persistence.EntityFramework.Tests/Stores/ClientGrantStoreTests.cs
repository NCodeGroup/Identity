// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientGrantStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string ResourceServerId = "rs-1";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore ClientStore { get; }
    private ResourceServerStore ResourceServerStore { get; }
    private ClientGrantStore Store { get; }

    public ClientGrantStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        ClientStore = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
        ResourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
        Store = new ClientGrantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedClientGrant CreateGrant(params string[] scopes) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = string.Empty,
            Scopes = scopes,
        };

    private async ValueTask SeedAsync(CancellationToken cancellationToken)
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder
        {
            TenantId = TenantId,
            DisplayName = "Tenant One",
        }.Build();
        await TenantStore.AddAsync(tenant, cancellationToken);

        // The tenant must be persisted before its dependents are added (an in-memory query does not observe
        // unsaved inserts).
        await DbContext.SaveChangesAsync(cancellationToken);

        var client = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = ClientId,
        }.Build();
        await ClientStore.AddAsync(client, cancellationToken);

        var resourceServer = new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
        }.Build();
        await ResourceServerStore.AddAsync(resourceServer, cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_RoundtripsScopes()
    {
        await SeedAsync(CancellationToken.None);

        await Store.AddAsync(CreateGrant("read:x", "write:x"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await Store.GetOrDefaultAsync(
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );

        Assert.NotNull(reloaded);
        Assert.Equal(new[] { "read:x", "write:x" }, reloaded.Scopes);
        Assert.False(string.IsNullOrEmpty(reloaded.ConcurrencyToken));
    }

    [Fact]
    public async Task GetPageAsync_ByClient_ReturnsGrant()
    {
        await SeedAsync(CancellationToken.None);
        await Store.AddAsync(CreateGrant("read:x"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetPageAsync(
            ClientId,
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Single(page.Items);
        Assert.Equal(ResourceServerId, page.Items[0].ResourceServerId);
    }

    [Fact]
    public async Task RemoveAsync_RemovesGrant()
    {
        await SeedAsync(CancellationToken.None);
        await Store.AddAsync(CreateGrant("read:x"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveAsync(ClientId, ResourceServerId, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var reloaded = await Store.GetOrDefaultAsync(
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenGrantExists_ReturnsTrue()
    {
        await SeedAsync(CancellationToken.None);
        await Store.AddAsync(CreateGrant("read:x"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var hasDependents = await ResourceServerStore.HasDependentsAsync(
            ResourceServerId,
            CancellationToken.None
        );

        Assert.True(hasDependents);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoGrant_ReturnsFalse()
    {
        await SeedAsync(CancellationToken.None);

        var hasDependents = await ResourceServerStore.HasDependentsAsync(
            ResourceServerId,
            CancellationToken.None
        );

        Assert.False(hasDependents);
    }
}
