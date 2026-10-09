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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
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
        _clientStore = new ClientStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
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
        await _tenantStore.AddAsync(
            new PersistedTenant
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
            },
            CancellationToken.None
        );

        // The tenant must be persisted before its dependents are added (an in-memory query does not observe
        // unsaved inserts).
        await _dbContext.SaveChangesAsync();

        await _clientStore.AddAsync(
            new PersistedClient
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                IsDisabled = false,
                Settings = new PersistedClientSettings
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    ConcurrencyToken = string.Empty,
                    Value = EmptyObject(),
                },
                Secrets = new PersistedClientSecrets
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    ConcurrencyToken = string.Empty,
                    Value = [],
                },
            },
            CancellationToken.None
        );

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
                Settings = EmptyObject(),
                Scopes = [],
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
            cursor: null,
            limit: 50,
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
