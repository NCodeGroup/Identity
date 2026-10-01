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

public sealed class ResourceServerStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";
    private const string Identifier = "https://api.example.com";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ResourceServerStore _store;

    public ResourceServerStoreTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        services.AddSingleton<IdValueGenerator>();
        services.AddSingleton<UseIdGeneratorConvention>();
        services.AddDbContext<OpenIdDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = services.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new ResourceServerStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedScope CreateScope(string value, bool isSystem = false) =>
        new()
        {
            Value = value,
            Description = $"{value} description",
            IsSystem = isSystem,
        };

    private static PersistedResourceServer CreateResourceServer(
        bool isSystem = false,
        params PersistedScope[] scopes
    ) =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Identifier = Identifier,
            ConcurrencyToken = string.Empty,
            Name = "Example API",
            IsSystem = isSystem,
            IsDisabled = false,
            Settings = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            Scopes = scopes,
        };

    private async Task SeedTenantAsync()
    {
        var tenant = new PersistedTenant
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
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        await _tenantStore.AddAsync(tenant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region AddAsync / GetOrDefaultAsync Tests

    [Fact]
    public async Task AddAsync_RoundtripsWithScopes()
    {
        await SeedTenantAsync();

        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("read:x"), CreateScope("write:x")]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(Identifier, reloaded.Identifier);
        Assert.Equal("Example API", reloaded.Name);
        Assert.Equal(2, reloaded.Scopes.Count);
        Assert.False(string.IsNullOrEmpty(reloaded.ConcurrencyToken));
    }

    [Fact]
    public async Task GetByIdentifierOrDefaultAsync_ReturnsMatch()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var found = await _store.GetByIdentifierOrDefaultAsync(Identifier, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(ResourceServerId, found.ResourceServerId);
    }

    [Fact]
    public async Task AddAsync_DenormalizesTenantIdOntoResourceServerAndScopes()
    {
        await SeedTenantAsync();
        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("read:x")]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // The denormalized tenant key is the normalized (lowercase) natural tenant id, stored on the entity itself
        // so tenant-scoping needs no join to the tenant table (ADR-0024 split-readiness).
        var expected = TenantId.ToLowerInvariant();

        var resourceServer = await _dbContext.ResourceServers.SingleAsync(entity =>
            entity.NormalizedResourceServerId == ResourceServerId.ToLowerInvariant()
        );
        Assert.Equal(expected, resourceServer.NormalizedTenantId);

        var scope = await _dbContext.Scopes.SingleAsync();
        Assert.Equal(expected, scope.NormalizedTenantId);
    }

    #endregion

    #region Scope Sub-Resource Tests

    [Fact]
    public async Task AddScopeAsync_AddsScope()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.AddScopeAsync(ResourceServerId, CreateScope("read:x"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded.Scopes);
        Assert.Equal("read:x", reloaded.Scopes.First().Value);
    }

    [Fact]
    public async Task RemoveScopeAsync_WhenSystemScope_Throws()
    {
        await SeedTenantAsync();
        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("openid", isSystem: true)]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveScopeAsync(ResourceServerId, "openid", CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveScopeAsync_RemovesNonSystemScope()
    {
        await SeedTenantAsync();
        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("read:x")]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveScopeAsync(
            ResourceServerId,
            "read:x",
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded.Scopes);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenSystem_Throws()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(isSystem: true), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(ResourceServerId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_RemovesNonSystem()
    {
        await SeedTenantAsync();
        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("read:x")]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.RemoveAsync(ResourceServerId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    #endregion
}
