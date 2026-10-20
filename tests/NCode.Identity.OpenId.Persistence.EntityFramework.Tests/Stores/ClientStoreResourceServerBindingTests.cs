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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// A client may act for a resource server of its tenant; the binding is a relational foreign key the client and
// resource-server stores both honor.
public sealed class ClientStoreResourceServerBindingTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";
    private const string OtherResourceServerId = "rs-2";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ResourceServerStore _resourceServerStore;
    private readonly ClientStore _store;

    public ClientStoreResourceServerBindingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _resourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
        _store = new ClientStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    #region Helpers

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedClient CreateClient(string clientId, string? resourceServerId) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            ResourceServerId = resourceServerId,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = EmptyObject(),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private static PersistedResourceServer CreateResourceServer(string resourceServerId) =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = resourceServerId,
            Identifier = $"https://{resourceServerId}.example.com",
            ConcurrencyToken = string.Empty,
            Name = resourceServerId,
            IsSystem = false,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Value = EmptyObject(),
            },
            Scopes = [],
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Value = [],
            },
        };

    private async Task SeedTenantAndResourceServersAsync()
    {
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = TenantId,
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
        };
        await _tenantStore.AddAsync(tenant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        await _resourceServerStore.AddAsync(
            CreateResourceServer(ResourceServerId),
            CancellationToken.None
        );
        await _resourceServerStore.AddAsync(
            CreateResourceServer(OtherResourceServerId),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task AddClientsAsync(params PersistedClient[] clients)
    {
        foreach (var client in clients)
        {
            await _store.AddAsync(client, CancellationToken.None);
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #endregion

    #region ClientStore Tests

    [Fact]
    public async Task AddAsync_WhenBoundToAResourceServer_RoundtripsTheBinding()
    {
        await SeedTenantAndResourceServersAsync();
        await AddClientsAsync(
            CreateClient("bound", ResourceServerId),
            CreateClient("ordinary", resourceServerId: null)
        );

        var bound = await _store.GetOrDefaultAsync("bound", CancellationToken.None);
        var ordinary = await _store.GetOrDefaultAsync("ordinary", CancellationToken.None);

        Assert.Equal(ResourceServerId, bound?.ResourceServerId);
        Assert.NotNull(ordinary);
        Assert.Null(ordinary.ResourceServerId);
    }

    [Fact]
    public async Task AddAsync_WhenTheResourceServerIsUnknown_Throws()
    {
        await SeedTenantAndResourceServersAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.AddAsync(CreateClient("bound", "rs-unknown"), CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetPageByResourceServerAsync_ReturnsOnlyTheClientsThatActForIt()
    {
        await SeedTenantAndResourceServersAsync();
        await AddClientsAsync(
            CreateClient("bound-1", ResourceServerId),
            CreateClient("other", OtherResourceServerId),
            CreateClient("ordinary", resourceServerId: null),
            CreateClient("bound-2", ResourceServerId),
            CreateClient("bound-3", ResourceServerId)
        );

        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var page = await _store.GetPageByResourceServerAsync(
                ResourceServerId,
                new PageRequest { Cursor = cursor, Limit = 2 },
                CancellationToken.None
            );
            seen.AddRange(page.Items.Select(client => client.ClientId));
            cursor = page.NextCursor;
        } while (cursor is not null);

        Assert.Equal(["bound-1", "bound-2", "bound-3"], seen);
    }

    #endregion

    #region ResourceServerStore Tests

    [Fact]
    public async Task HasDependentsAsync_WhenAClientActsForTheResourceServer_ReturnsTrue()
    {
        await SeedTenantAndResourceServersAsync();
        await AddClientsAsync(CreateClient("bound", ResourceServerId));

        var hasDependents = await _resourceServerStore.HasDependentsAsync(
            ResourceServerId,
            CancellationToken.None
        );
        var otherHasDependents = await _resourceServerStore.HasDependentsAsync(
            OtherResourceServerId,
            CancellationToken.None
        );

        Assert.True(hasDependents);
        Assert.False(otherHasDependents);
    }

    [Fact]
    public async Task RemoveAsync_WhenAClientActsForTheResourceServer_Throws()
    {
        await SeedTenantAndResourceServersAsync();
        await AddClientsAsync(CreateClient("bound", ResourceServerId));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _resourceServerStore.RemoveAsync(ResourceServerId, CancellationToken.None)
        );
    }

    #endregion
}
