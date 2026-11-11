// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class RemoteKeyReferenceStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Provider = "aws-kms";

    private ServiceProvider ServiceProvider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore ClientStore { get; }
    private RemoteKeyReferenceStore Store { get; }

    public RemoteKeyReferenceStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        ServiceProvider = serviceCollection.BuildServiceProvider();
        DbContext = ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = ServiceProvider.GetRequiredService<IIdGenerator<long>>();
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        ClientStore = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext,
            TestCryptoServices.CreateResourceIdGenerator()
        );
        Store = new RemoteKeyReferenceStore(Mock.Of<IStoreProvider>(), DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        ServiceProvider.Dispose();
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

    private async ValueTask SeedTenantAsync(
        string tenantId,
        PersistedSecret[] secrets,
        CancellationToken cancellationToken
    )
    {
        DbContext.SeedTeam(ServiceProvider.GetRequiredService<IIdGenerator<long>>(), "platform");
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

    #region GetKeyIdPageAsync Tests

    [Fact]
    public async Task GetKeyIdPage_TenantAndClientRemoteSecrets_ReturnsTheirKeyIds()
    {
        await SeedTenantAsync(
            TenantId,
            [CreateSecret("tenant-secret", $"{Provider}:key-1")],
            CancellationToken.None
        );
        await SeedClientAsync(
            TenantId,
            "client-1",
            [CreateSecret("client-secret", $"{Provider}:key-2")],
            CancellationToken.None
        );

        var page = await Store.GetKeyIdPageAsync(
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
            [
                CreateSecret("matching-secret", $"{Provider}:key-1"),
                CreateSecret("other-provider-secret", "azure-key-vault:key-2"),
                CreateSecret("similar-provider-secret", $"{Provider}-2:key-3"),
                CreateSecret("pkcs8-secret", $"{Provider}:key-4", SecretEncodings.Pkcs8),
            ],
            CancellationToken.None
        );

        var page = await Store.GetKeyIdPageAsync(
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
            [
                CreateSecret("secret-a", $"{Provider}:key-1"),
                CreateSecret("secret-b", $"{Provider}:key-2"),
            ],
            CancellationToken.None
        );

        var first = await Store.GetKeyIdPageAsync(
            Provider,
            new PageRequest { Limit = 1 },
            CancellationToken.None
        );

        Assert.Single(first.Items);
        Assert.NotNull(first.NextCursor);

        var second = await Store.GetKeyIdPageAsync(
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
        var page = await Store.GetKeyIdPageAsync(
            Provider,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    #endregion
}
