// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class RemoteKeyReferenceStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Provider = "aws-kms";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _clientStore;
    private readonly RemoteKeyReferenceStore _store;

    public RemoteKeyReferenceStoreTests()
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
        _store = new RemoteKeyReferenceStore(Mock.Of<IStoreProvider>(), _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedSecret CreateSecret(
        string secretId,
        string encodedValue,
        string secretEncoding = SecretEncodings.Remote
    ) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            SecretEncoding = secretEncoding,
            EncodedValue = encodedValue,
        }.Build();

    private async Task SeedTenantAsync(string tenantId, params PersistedSecret[] secrets)
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder { TenantId = tenantId, Secrets = secrets }.Build();
        await _tenantStore.AddAsync(tenant, CancellationToken.None);
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

    #region GetKeyIdPageAsync Tests

    [Fact]
    public async Task GetKeyIdPage_TenantAndClientRemoteSecrets_ReturnsTheirKeyIds()
    {
        await SeedTenantAsync(TenantId, CreateSecret("tenant-secret", $"{Provider}:key-1"));
        await SeedClientAsync(
            TenantId,
            "client-1",
            CreateSecret("client-secret", $"{Provider}:key-2")
        );

        var page = await _store.GetKeyIdPageAsync(
            Provider,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(["key-1", "key-2"], page.Items.Order().ToArray());
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetKeyIdPage_OtherProviderAndNonRemoteSecrets_SkipsThem()
    {
        await SeedTenantAsync(
            TenantId,
            CreateSecret("matching-secret", $"{Provider}:key-1"),
            CreateSecret("other-provider-secret", "azure-key-vault:key-2"),
            CreateSecret("similar-provider-secret", $"{Provider}-2:key-3"),
            CreateSecret("pkcs8-secret", $"{Provider}:key-4", SecretEncodings.Pkcs8)
        );

        var page = await _store.GetKeyIdPageAsync(
            Provider,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(["key-1"], page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetKeyIdPage_LimitOfOne_PagesAcrossTwoPages()
    {
        await SeedTenantAsync(
            TenantId,
            CreateSecret("secret-a", $"{Provider}:key-1"),
            CreateSecret("secret-b", $"{Provider}:key-2")
        );

        var first = await _store.GetKeyIdPageAsync(
            Provider,
            new PageRequest { Limit = 1 },
            CancellationToken.None
        );

        Assert.Single(first.Items);
        Assert.NotNull(first.NextCursor);

        var second = await _store.GetKeyIdPageAsync(
            Provider,
            new PageRequest { Limit = 1, Cursor = first.NextCursor },
            CancellationToken.None
        );

        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        Assert.Equal(["key-1", "key-2"], first.Items.Concat(second.Items).Order().ToArray());
    }

    [Fact]
    public async Task GetKeyIdPage_NoSecrets_ReturnsEmptyPage()
    {
        var page = await _store.GetKeyIdPageAsync(
            Provider,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    #endregion
}
