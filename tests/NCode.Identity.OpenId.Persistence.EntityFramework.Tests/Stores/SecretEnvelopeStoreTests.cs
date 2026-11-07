// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class SecretEnvelopeStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string OtherTenantId = "tenant-2";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _clientStore;
    private readonly ResourceServerStore _resourceServerStore;
    private readonly SecretEnvelopeStore _store;

    public SecretEnvelopeStoreTests()
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
        _store = new SecretEnvelopeStore(Mock.Of<IStoreProvider>(), _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedSecret CreateSecret(string secretId, string encodedValue) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = string.Empty,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            Origin = SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = encodedValue,
        };

    private async Task SeedTenantAsync(string tenantId, params PersistedSecret[] secrets)
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        await _tenantStore.AddAsync(
            new PersistedTenant
            {
                TenantId = tenantId,
                TeamId = "platform",
                ConcurrencyToken = string.Empty,
                Issuer = null,
                IsDisabled = false,
                DisplayName = tenantId,
                Settings = new PersistedTenantSettings
                {
                    TenantId = tenantId,
                    ConcurrencyToken = string.Empty,
                    Value = EmptyObject(),
                },
                Secrets = new PersistedTenantSecrets
                {
                    TenantId = tenantId,
                    ConcurrencyToken = string.Empty,
                    Value = secrets,
                },
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedClientAsync(
        string tenantId,
        string clientId,
        params PersistedSecret[] secrets
    )
    {
        var client = new PersistedClientBuilder
        {
            TenantId = tenantId,
            ClientId = clientId,
            Secrets = secrets,
        }.Build();

        await _clientStore.AddAsync(client, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedResourceServerAsync(
        string tenantId,
        string resourceServerId,
        params PersistedSecret[] secrets
    )
    {
        await _resourceServerStore.AddAsync(
            new PersistedResourceServer
            {
                TenantId = tenantId,
                ResourceServerId = resourceServerId,
                Identifier = $"https://{resourceServerId}.example.com",
                ConcurrencyToken = string.Empty,
                Name = resourceServerId,
                IsSystem = false,
                IsDisabled = false,
                Settings = new PersistedResourceServerSettings
                {
                    TenantId = tenantId,
                    ResourceServerId = resourceServerId,
                    Value = EmptyObject(),
                },
                Scopes = [],
                Secrets = new PersistedResourceServerSecrets
                {
                    TenantId = tenantId,
                    ResourceServerId = resourceServerId,
                    Value = secrets,
                },
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    // Every seeded value starts with this letter.
    private static readonly string[] AnyValue = ["v"];

    private async Task<IReadOnlyList<PersistedProtectedValue>> ReadAllAsync(SecretOwner owner)
    {
        var all = new List<PersistedProtectedValue>();
        string? cursor = null;
        do
        {
            var page = await _store.GetPageAsync(
                owner,
                AnyValue,
                new PageRequest { Limit = 2, Cursor = cursor },
                CancellationToken.None
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
        await SeedTenantAsync(TenantId, CreateSecret("tenant-secret", "v1"));
        await SeedTenantAsync(OtherTenantId, CreateSecret("other-secret", "v2"));
        await SeedClientAsync(TenantId, "client-1", CreateSecret("client-secret", "v3"));
        await SeedClientAsync(OtherTenantId, "client-2", CreateSecret("other-client", "v4"));

        var values = await ReadAllAsync(SecretOwner.ForTenant("TENANT-1"));

        Assert.Equal(["client-secret", "tenant-secret"], values.Select(x => x.RecordId).Order());
        Assert.All(values, x => Assert.Equal(x.RecordId, x.ItemId));
        Assert.All(values, x => Assert.False(string.IsNullOrEmpty(x.ConcurrencyToken)));
    }

    [Fact]
    public async Task GetPage_TenantOwner_ReturnsTheSecretsOfItsResourceServers()
    {
        await SeedTenantAsync(TenantId);
        await SeedTenantAsync(OtherTenantId);
        await SeedResourceServerAsync(TenantId, "rs-1", CreateSecret("rs-secret", "v1"));
        await SeedResourceServerAsync(OtherTenantId, "rs-2", CreateSecret("other-rs-secret", "v2"));

        var values = await ReadAllAsync(SecretOwner.ForTenant(TenantId));

        var value = Assert.Single(values);
        Assert.Equal("rs-secret", value.RecordId);
    }

    [Fact]
    public async Task GetPage_PlatformOwner_ReturnsEmpty()
    {
        await SeedTenantAsync(TenantId, CreateSecret("tenant-secret", "v1"));

        var page = await _store.GetPageAsync(
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
        await SeedTenantAsync(TenantId, CreateSecret("tenant-secret", "v1"));

        var page = await _store.GetPageAsync(
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
            CreateSecret("s1", "aa-one"),
            CreateSecret("s2", "bb-two"),
            CreateSecret("s3", "cc-three"),
            CreateSecret("s4", "a")
        );

        var page = await _store.GetPageAsync(
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
            _store
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
            _store
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
            CreateSecret("s1", "v1"),
            CreateSecret("s2", "v2"),
            CreateSecret("s3", "v3")
        );

        var values = await ReadAllAsync(SecretOwner.ForTenant(TenantId));

        Assert.Equal(3, values.Count);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Update_WritesTheValueAndChangesTheOwnersSecretsToken()
    {
        await SeedTenantAsync(TenantId, CreateSecret("tenant-secret", "v1"));
        var before = (
            await _dbContext.Tenants.AsNoTracking().SingleAsync(x => x.TenantId == TenantId)
        ).SecretsConcurrencyToken;
        var value = Assert.Single(await ReadAllAsync(SecretOwner.ForTenant(TenantId)));
        value.ProtectedValue = "v1-rewrapped";

        await _store.UpdateAsync(value, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = Assert.Single(await ReadAllAsync(SecretOwner.ForTenant(TenantId)));
        Assert.Equal("v1-rewrapped", reloaded.ProtectedValue);
        Assert.NotEqual(value.ConcurrencyToken, reloaded.ConcurrencyToken);
        var after = (
            await _dbContext.Tenants.AsNoTracking().SingleAsync(x => x.TenantId == TenantId)
        ).SecretsConcurrencyToken;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Update_ClientSecret_ChangesTheClientsSecretsToken()
    {
        await SeedTenantAsync(TenantId);
        await SeedClientAsync(TenantId, "client-1", CreateSecret("client-secret", "v1"));
        var before = (
            await _dbContext
                .Clients.AsNoTracking()
                .SingleAsync(x => x.NormalizedClientId == "client-1")
        ).SecretsConcurrencyToken;
        var value = Assert.Single(await ReadAllAsync(SecretOwner.ForTenant(TenantId)));
        value.ProtectedValue = "rewrapped";

        await _store.UpdateAsync(value, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var after = (
            await _dbContext
                .Clients.AsNoTracking()
                .SingleAsync(x => x.NormalizedClientId == "client-1")
        ).SecretsConcurrencyToken;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Update_ResourceServerSecret_ChangesTheResourceServersSecretsToken()
    {
        await SeedTenantAsync(TenantId);
        await SeedResourceServerAsync(TenantId, "rs-1", CreateSecret("rs-secret", "v1"));
        var before = (
            await _dbContext
                .ResourceServers.AsNoTracking()
                .SingleAsync(x => x.NormalizedResourceServerId == "rs-1")
        ).SecretsConcurrencyToken;
        var value = Assert.Single(await ReadAllAsync(SecretOwner.ForTenant(TenantId)));
        value.ProtectedValue = "rewrapped";

        await _store.UpdateAsync(value, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var after = (
            await _dbContext
                .ResourceServers.AsNoTracking()
                .SingleAsync(x => x.NormalizedResourceServerId == "rs-1")
        ).SecretsConcurrencyToken;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Update_WhenTheTokenIsStale_Throws()
    {
        await SeedTenantAsync(TenantId, CreateSecret("tenant-secret", "v1"));
        var value = Assert.Single(await ReadAllAsync(SecretOwner.ForTenant(TenantId)));

        await Assert.ThrowsAsync<StoreConcurrencyException>(() =>
            _store
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
        var written = await _store.UpdateAsync(
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
