// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class SecretEnvelopeStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string OtherTenantId = "tenant-2";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore ClientStore { get; }
    private ResourceServerStore ResourceServerStore { get; }
    private SecretEnvelopeStore Store { get; }

    public SecretEnvelopeStoreTests()
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
        Store = new SecretEnvelopeStore(Mock.Of<IStoreProvider>(), DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedSecret CreateSecret(string secretId, string encodedValue) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = encodedValue,
        }.Build();

    private async ValueTask SeedTenantAsync(
        string tenantId,
        PersistedSecret[] secrets,
        CancellationToken cancellationToken
    )
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder { TenantId = tenantId, Secrets = secrets }.Build();
        await TenantStore.AddAsync(tenant, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask SeedClientAsync(
        string tenantId,
        string clientId,
        PersistedSecret[] secrets,
        CancellationToken cancellationToken
    )
    {
        var client = new PersistedClientBuilder
        {
            TenantId = tenantId,
            ClientId = clientId,
            Secrets = secrets,
        }.Build();

        await ClientStore.AddAsync(client, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask SeedResourceServerAsync(
        string tenantId,
        string resourceServerId,
        PersistedSecret[] secrets,
        CancellationToken cancellationToken
    )
    {
        var resourceServer = new PersistedResourceServerBuilder
        {
            TenantId = tenantId,
            ResourceServerId = resourceServerId,
            Identifier = $"https://{resourceServerId}.example.com",
            Name = resourceServerId,
            Secrets = secrets,
        }.Build();
        await ResourceServerStore.AddAsync(resourceServer, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    // Every seeded value starts with this letter.
    private static string[] AnyValue { get; } = ["v"];

    private async ValueTask<IReadOnlyList<PersistedProtectedValue>> ReadAllAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var all = new List<PersistedProtectedValue>();
        string? cursor = null;
        do
        {
            var page = await Store.GetPageAsync(
                owner,
                AnyValue,
                new PageRequest { Limit = 2, Cursor = cursor },
                cancellationToken
            );
            all.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        return all;
    }

    #region GetPageAsync Tests

    [Fact]
    public async Task GetPage_TenantOwner_ReturnsTenantAndClientSecretsOfThatTenantOnly()
    {
        await SeedTenantAsync(
            TenantId,
            [CreateSecret("tenant-secret", "v1")],
            CancellationToken.None
        );
        await SeedTenantAsync(
            OtherTenantId,
            [CreateSecret("other-secret", "v2")],
            CancellationToken.None
        );
        await SeedClientAsync(
            TenantId,
            "client-1",
            [CreateSecret("client-secret", "v3")],
            CancellationToken.None
        );
        await SeedClientAsync(
            OtherTenantId,
            "client-2",
            [CreateSecret("other-client", "v4")],
            CancellationToken.None
        );

        var values = await ReadAllAsync(SecretOwner.ForTenant("TENANT-1"), CancellationToken.None);

        Assert.Equal(["client-secret", "tenant-secret"], values.Select(x => x.RecordId).Order());
        Assert.All(values, x => Assert.Equal(x.RecordId, x.ItemId));
        Assert.All(values, x => Assert.False(string.IsNullOrEmpty(x.ConcurrencyToken)));
    }

    [Fact]
    public async Task GetPage_TenantOwner_ReturnsTheSecretsOfItsResourceServers()
    {
        await SeedTenantAsync(TenantId, [], CancellationToken.None);
        await SeedTenantAsync(OtherTenantId, [], CancellationToken.None);
        await SeedResourceServerAsync(
            TenantId,
            "rs-1",
            [CreateSecret("rs-secret", "v1")],
            CancellationToken.None
        );
        await SeedResourceServerAsync(
            OtherTenantId,
            "rs-2",
            [CreateSecret("other-rs-secret", "v2")],
            CancellationToken.None
        );

        var values = await ReadAllAsync(SecretOwner.ForTenant(TenantId), CancellationToken.None);

        var value = Assert.Single(values);
        Assert.Equal("rs-secret", value.RecordId);
    }

    [Fact]
    public async Task GetPage_PlatformOwner_ReturnsEmpty()
    {
        await SeedTenantAsync(
            TenantId,
            [CreateSecret("tenant-secret", "v1")],
            CancellationToken.None
        );

        var page = await Store.GetPageAsync(
            SecretOwner.Platform,
            AnyValue,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPage_UnknownOwnerType_ReturnsEmpty()
    {
        await SeedTenantAsync(
            TenantId,
            [CreateSecret("tenant-secret", "v1")],
            CancellationToken.None
        );

        var page = await Store.GetPageAsync(
            new SecretOwner("custom", TenantId),
            AnyValue,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPage_ReturnsOnlyTheValuesThatStartWithAPrefix()
    {
        await SeedTenantAsync(
            TenantId,
            [
                CreateSecret("s1", "aa-one"),
                CreateSecret("s2", "bb-two"),
                CreateSecret("s3", "cc-three"),
                CreateSecret("s4", "a"),
            ],
            CancellationToken.None
        );

        var page = await Store.GetPageAsync(
            SecretOwner.ForTenant(TenantId),
            ["aa", "bb"],
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(["s1", "s2"], page.Items.Select(x => x.RecordId).Order());
    }

    [Fact]
    public async Task GetPage_WithPrefixesOfDifferentLengths_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store
                .GetPageAsync(
                    SecretOwner.ForTenant(TenantId),
                    ["aa", "b"],
                    new PageRequest { Limit = 10 },
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task GetPage_WithoutPrefixes_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store
                .GetPageAsync(
                    SecretOwner.ForTenant(TenantId),
                    [],
                    new PageRequest { Limit = 10 },
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task GetPage_PagesAcrossTheLimit()
    {
        await SeedTenantAsync(
            TenantId,
            [CreateSecret("s1", "v1"), CreateSecret("s2", "v2"), CreateSecret("s3", "v3")],
            CancellationToken.None
        );

        var values = await ReadAllAsync(SecretOwner.ForTenant(TenantId), CancellationToken.None);

        Assert.Equal(3, values.Count);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Update_WritesTheValueAndChangesTheOwnersSecretsToken()
    {
        await SeedTenantAsync(
            TenantId,
            [CreateSecret("tenant-secret", "v1")],
            CancellationToken.None
        );
        var before = (
            await DbContext.Tenants.AsNoTracking().SingleAsync(x => x.TenantId == TenantId)
        ).SecretsConcurrencyToken;
        var value = Assert.Single(
            await ReadAllAsync(SecretOwner.ForTenant(TenantId), CancellationToken.None)
        );
        value.ProtectedValue = "v1-rewrapped";

        await Store.UpdateAsync(value, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = Assert.Single(
            await ReadAllAsync(SecretOwner.ForTenant(TenantId), CancellationToken.None)
        );
        Assert.Equal("v1-rewrapped", reloaded.ProtectedValue);
        Assert.NotEqual(value.ConcurrencyToken, reloaded.ConcurrencyToken);
        var after = (
            await DbContext.Tenants.AsNoTracking().SingleAsync(x => x.TenantId == TenantId)
        ).SecretsConcurrencyToken;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Update_ClientSecret_ChangesTheClientsSecretsToken()
    {
        await SeedTenantAsync(TenantId, [], CancellationToken.None);
        await SeedClientAsync(
            TenantId,
            "client-1",
            [CreateSecret("client-secret", "v1")],
            CancellationToken.None
        );
        var before = (
            await DbContext
                .Clients.AsNoTracking()
                .SingleAsync(x => x.NormalizedClientId == "client-1")
        ).SecretsConcurrencyToken;
        var value = Assert.Single(
            await ReadAllAsync(SecretOwner.ForTenant(TenantId), CancellationToken.None)
        );
        value.ProtectedValue = "rewrapped";

        await Store.UpdateAsync(value, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var after = (
            await DbContext
                .Clients.AsNoTracking()
                .SingleAsync(x => x.NormalizedClientId == "client-1")
        ).SecretsConcurrencyToken;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Update_ResourceServerSecret_ChangesTheResourceServersSecretsToken()
    {
        await SeedTenantAsync(TenantId, [], CancellationToken.None);
        await SeedResourceServerAsync(
            TenantId,
            "rs-1",
            [CreateSecret("rs-secret", "v1")],
            CancellationToken.None
        );
        var before = (
            await DbContext
                .ResourceServers.AsNoTracking()
                .SingleAsync(x => x.NormalizedResourceServerId == "rs-1")
        ).SecretsConcurrencyToken;
        var value = Assert.Single(
            await ReadAllAsync(SecretOwner.ForTenant(TenantId), CancellationToken.None)
        );
        value.ProtectedValue = "rewrapped";

        await Store.UpdateAsync(value, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var after = (
            await DbContext
                .ResourceServers.AsNoTracking()
                .SingleAsync(x => x.NormalizedResourceServerId == "rs-1")
        ).SecretsConcurrencyToken;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Update_WhenTheTokenIsStale_Throws()
    {
        await SeedTenantAsync(
            TenantId,
            [CreateSecret("tenant-secret", "v1")],
            CancellationToken.None
        );
        var value = Assert.Single(
            await ReadAllAsync(SecretOwner.ForTenant(TenantId), CancellationToken.None)
        );

        await Assert.ThrowsAsync<StoreConcurrencyException>(() =>
            Store
                .UpdateAsync(
                    new PersistedProtectedValue
                    {
                        RecordId = value.RecordId,
                        ItemId = value.ItemId,
                        ProtectedValue = "x",
                        ConcurrencyToken = "stale",
                    },
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task Update_WhenTheSecretIsMissing_ReturnsFalse()
    {
        var written = await Store.UpdateAsync(
            new PersistedProtectedValue
            {
                RecordId = "missing",
                ItemId = "missing",
                ProtectedValue = "x",
                ConcurrencyToken = "x",
            },
            CancellationToken.None
        );

        Assert.False(written);
    }

    #endregion
}
