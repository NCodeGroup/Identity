// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ResourceServerStore ResourceServerStore { get; }
    private ClientStore Store { get; }

    public ClientStoreResourceServerBindingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        ResourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext
        );
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

    private static PersistedClient CreateClient(string clientId, string? resourceServerId) =>
        new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            ResourceServerId = resourceServerId,
        }.Build();

    private static PersistedResourceServer CreateResourceServer(string resourceServerId) =>
        new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = resourceServerId,
            Identifier = $"https://{resourceServerId}.example.com",
            Name = resourceServerId,
        }.Build();

    private async Task SeedTenantAndResourceServersAsync()
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder { TenantId = TenantId }.Build();
        await TenantStore.AddAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        await ResourceServerStore.AddAsync(
            CreateResourceServer(ResourceServerId),
            CancellationToken.None
        );
        await ResourceServerStore.AddAsync(
            CreateResourceServer(OtherResourceServerId),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task AddClientsAsync(params PersistedClient[] clients)
    {
        foreach (var client in clients)
        {
            await Store.AddAsync(client, CancellationToken.None);
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
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

        var bound = await Store.GetOrDefaultAsync("bound", CancellationToken.None);
        var ordinary = await Store.GetOrDefaultAsync("ordinary", CancellationToken.None);

        Assert.Equal(ResourceServerId, bound?.ResourceServerId);
        Assert.NotNull(ordinary);
        Assert.Null(ordinary.ResourceServerId);
    }

    [Fact]
    public async Task AddAsync_WhenTheResourceServerIsUnknown_Throws()
    {
        await SeedTenantAndResourceServersAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.AddAsync(CreateClient("bound", "rs-unknown"), CancellationToken.None)
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
            var page = await Store.GetPageByResourceServerAsync(
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

        var hasDependents = await ResourceServerStore.HasDependentsAsync(
            ResourceServerId,
            CancellationToken.None
        );
        var otherHasDependents = await ResourceServerStore.HasDependentsAsync(
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
            await ResourceServerStore.RemoveAsync(ResourceServerId, CancellationToken.None)
        );
    }

    #endregion
}
