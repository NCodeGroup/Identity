// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
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

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _clientStore;
    private readonly ResourceServerStore _resourceServerStore;
    private readonly ClientGrantStore _store;

    public ClientGrantStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _clientStore = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
        _resourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
        _store = new ClientGrantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedClientGrant CreateGrant(params string[] scopes) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = string.Empty,
            Scopes = scopes,
        };

    private async Task SeedAsync()
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        await _tenantStore.AddAsync(
            new PersistedTenant
            {
                TenantId = TenantId,
                TeamId = "platform",
                ConcurrencyToken = string.Empty,
                Issuer = null,
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
            },
            CancellationToken.None
        );

        // The tenant must be persisted before its dependents are added (an in-memory query does not observe
        // unsaved inserts).
        await _dbContext.SaveChangesAsync();

        var client = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = ClientId,
        }.Build();
        await _clientStore.AddAsync(client, CancellationToken.None);

        await _resourceServerStore.AddAsync(
            new PersistedResourceServer
            {
                TenantId = TenantId,
                ResourceServerId = ResourceServerId,
                Identifier = "https://api.example.com",
                ConcurrencyToken = string.Empty,
                Name = "Example API",
                IsSystem = false,
                IsDisabled = false,
                Settings = new PersistedResourceServerSettings
                {
                    TenantId = TenantId,
                    ResourceServerId = ResourceServerId,
                    Value = EmptyObject(),
                },
                Scopes = [],
                Secrets = new PersistedResourceServerSecrets
                {
                    TenantId = TenantId,
                    ResourceServerId = ResourceServerId,
                    Value = [],
                },
            },
            CancellationToken.None
        );

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_RoundtripsScopes()
    {
        await SeedAsync();

        await _store.AddAsync(CreateGrant("read:x", "write:x"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(
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
        await SeedAsync();
        await _store.AddAsync(CreateGrant("read:x"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var page = await _store.GetPageAsync(
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
        await SeedAsync();
        await _store.AddAsync(CreateGrant("read:x"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveAsync(ClientId, ResourceServerId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenGrantExists_ReturnsTrue()
    {
        await SeedAsync();
        await _store.AddAsync(CreateGrant("read:x"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var hasDependents = await _resourceServerStore.HasDependentsAsync(
            ResourceServerId,
            CancellationToken.None
        );

        Assert.True(hasDependents);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoGrant_ReturnsFalse()
    {
        await SeedAsync();

        var hasDependents = await _resourceServerStore.HasDependentsAsync(
            ResourceServerId,
            CancellationToken.None
        );

        Assert.False(hasDependents);
    }
}
