// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
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

    private readonly SwitchableAmbientTenantAccessor _ambient = new();
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _store;

    public ClientStorePagingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IAmbientTenantAccessor>(_ambient);
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
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

    private async Task SeedAsync(int count)
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            TeamId = "platform",
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = TenantId,
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

        for (var index = 1; index <= count; index++)
        {
            await _store.AddAsync(CreateClient($"client-{index}"), CancellationToken.None);
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetPageAsync_TraversesEveryItemExactlyOnce()
    {
        await SeedAsync(5);

        var seen = new List<string>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await _store.GetPageAsync(
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
        await SeedAsync(3);

        var page = await _store.GetPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPageAsync_WhenCursorIsMalformed_StartsFromBeginning()
    {
        await SeedAsync(3);

        var page = await _store.GetPageAsync(
            new PageRequest { Cursor = "not-a-valid-cursor", Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetPageAsync_WhenEmpty_ReturnsEmptyPageAndNullCursor()
    {
        var page = await _store.GetPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    #region GetSettingsPageAsync Tests

    [Fact]
    public async Task GetSettingsPageAsync_TraversesEveryClientExactlyOnce()
    {
        await SeedAsync(5);

        var seen = new List<string>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await _store.GetSettingsPageAsync(
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
        await SeedAsync(3);

        var page = await _store.GetSettingsPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    #endregion

    #region GetPageByOwningNodeAsync Tests

    [Fact]
    public async Task GetPageByOwningNodeAsync_ListsOnlyTheClientsTheNodeOwns()
    {
        await SeedAsync(1);
        await _store.AddAsync(
            CreateClient("team-client-1", owningNodeType: "team", owningNodeId: "team-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateClient("team-client-2", owningNodeType: "team", owningNodeId: "TEAM-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateClient("other-team-client", owningNodeType: "team", owningNodeId: "team-2"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateClient("platform-client", owningNodeType: "platform", owningNodeId: "platform"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var teamPage = await _store.GetPageByOwningNodeAsync(
            "team",
            "team-1",
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );
        var platformPage = await _store.GetPageByOwningNodeAsync(
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
        await SeedAsync(1);
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _dbContext.SeedTenant(idGenerator, "tenant-2");
        var otherClient = CreateClient("other-client", tenantId: "tenant-2");
        await _store.AddAsync(otherClient, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        var principalId = Assert.IsType<string>(otherClient.PrincipalId);

        using var scope = _ambient.BeginScope(TenantId);
        var loaded = await _store.GetByPrincipalOrDefaultAsync(principalId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("tenant-2", loaded.TenantId);
        Assert.Equal("other-client", loaded.ClientId);
        Assert.Equal(principalId, loaded.PrincipalId);
    }

    [Fact]
    public async Task GetByPrincipalOrDefaultAsync_WhenNoClientHoldsThePrincipal_ReturnsNull()
    {
        await SeedAsync(2);

        var loaded = await _store.GetByPrincipalOrDefaultAsync(
            "unknown-principal",
            CancellationToken.None
        );

        Assert.Null(loaded);
    }

    #endregion
}
