// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStorePagingTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private SwitchableAmbientTenantAccessor Ambient { get; } = new();
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore Store { get; }

    public ClientStorePagingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IAmbientTenantAccessor>(Ambient);
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
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

    private static PersistedClient CreateClient(
        string clientId,
        string tenantId = TenantId,
        string? owningNodeType = null,
        string? owningNodeId = null
    ) =>
        new PersistedClientBuilder
        {
            TenantId = tenantId,
            ClientId = clientId,
            OwningNodeType = owningNodeType,
            OwningNodeId = owningNodeId,
        }.Build();

    private async ValueTask SeedAsync(int count, CancellationToken cancellationToken)
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder { TenantId = TenantId }.Build();
        await TenantStore.AddAsync(tenant, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        for (var index = 1; index <= count; index++)
        {
            await Store.AddAsync(CreateClient($"client-{index}"), cancellationToken);
        }
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetPageAsync_TraversesEveryItemExactlyOnce()
    {
        await SeedAsync(5, CancellationToken.None);

        var seen = new List<string>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await Store.GetPageAsync(
                new PageRequest { Cursor = cursor, Limit = 2 },
                CancellationToken.None
            );
            Assert.True(page.Items.Count <= 2);
            seen.AddRange(page.Items.Select(client => client.ClientId));
            cursor = page.NextCursor;
            pageCount++;
        } while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(5, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task GetPageAsync_WhenAllFitOnOnePage_ReturnsNullCursor()
    {
        await SeedAsync(3, CancellationToken.None);

        var page = await Store.GetPageAsync(new PageRequest { Limit = 10 }, CancellationToken.None);

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPageAsync_WhenCursorIsMalformed_StartsFromBeginning()
    {
        await SeedAsync(3, CancellationToken.None);

        var page = await Store.GetPageAsync(
            new PageRequest { Cursor = "not-a-valid-cursor", Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetPageAsync_WhenEmpty_ReturnsEmptyPageAndNullCursor()
    {
        var page = await Store.GetPageAsync(new PageRequest { Limit = 10 }, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    #region GetSettingsPageAsync Tests

    [Fact]
    public async Task GetSettingsPageAsync_TraversesEveryClientExactlyOnce()
    {
        await SeedAsync(5, CancellationToken.None);

        var seen = new List<string>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await Store.GetSettingsPageAsync(
                new PageRequest { Cursor = cursor, Limit = 2 },
                CancellationToken.None
            );
            Assert.True(page.Items.Count <= 2);
            foreach (var settings in page.Items)
            {
                Assert.Equal(TenantId, settings.TenantId);
                Assert.False(string.IsNullOrEmpty(settings.ConcurrencyToken));
                seen.Add(settings.ClientId);
            }
            cursor = page.NextCursor;
            pageCount++;
        } while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(5, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        for (var index = 1; index <= 5; index++)
        {
            Assert.Contains($"client-{index}", seen);
        }
    }

    [Fact]
    public async Task GetSettingsPageAsync_ReadsWithoutTracking()
    {
        await SeedAsync(3, CancellationToken.None);

        var page = await Store.GetSettingsPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
        Assert.Empty(DbContext.ChangeTracker.Entries());
    }

    #endregion

    #region GetPageByOwningNodeAsync Tests

    [Fact]
    public async Task GetPageByOwningNodeAsync_ListsOnlyTheClientsTheNodeOwns()
    {
        await SeedAsync(1, CancellationToken.None);
        await Store.AddAsync(
            CreateClient("team-client-1", owningNodeType: "team", owningNodeId: "team-1"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateClient("team-client-2", owningNodeType: "team", owningNodeId: "TEAM-1"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateClient("other-team-client", owningNodeType: "team", owningNodeId: "team-2"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateClient("platform-client", owningNodeType: "platform", owningNodeId: "platform"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var teamPage = await Store.GetPageByOwningNodeAsync(
            "team",
            "team-1",
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );
        var platformPage = await Store.GetPageByOwningNodeAsync(
            "platform",
            "platform",
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        var teamClientIds = teamPage.Items.Select(client => client.ClientId).ToList();
        Assert.Equal(["team-client-1", "team-client-2"], teamClientIds);
        Assert.Null(teamPage.NextCursor);
        var platformClient = Assert.Single(platformPage.Items);
        Assert.Equal("platform-client", platformClient.ClientId);
    }

    #endregion

    #region GetByPrincipalOrDefaultAsync Tests

    [Fact]
    public async Task GetByPrincipalOrDefaultAsync_FindsTheClientInAnotherTenant()
    {
        await SeedAsync(1, CancellationToken.None);
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        DbContext.SeedTenant(idGenerator, "tenant-2");
        var otherClient = CreateClient("other-client", tenantId: "tenant-2");
        await Store.AddAsync(otherClient, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        var principalId = Assert.IsType<string>(otherClient.PrincipalId);

        using var scope = Ambient.BeginScope(TenantId);
        var loaded = await Store.GetByPrincipalOrDefaultAsync(principalId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("tenant-2", loaded.TenantId);
        Assert.Equal("other-client", loaded.ClientId);
        Assert.Equal(principalId, loaded.PrincipalId);
    }

    [Fact]
    public async Task GetByPrincipalOrDefaultAsync_WhenNoClientHoldsThePrincipal_ReturnsNull()
    {
        await SeedAsync(2, CancellationToken.None);

        var loaded = await Store.GetByPrincipalOrDefaultAsync(
            "unknown-principal",
            CancellationToken.None
        );

        Assert.Null(loaded);
    }

    #endregion
}
