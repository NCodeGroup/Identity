// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates ClientStore.RemoveAsync against a relational provider that enforces foreign keys (ADR-1212). The EF
// InMemory provider skips foreign keys, so the delete order and the exact set of rows removed are proven here.
public sealed class ClientStoreRelationalRemovalTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";
    private const string ClientOneId = "client-1";
    private const string ClientTwoId = "client-2";
    private const string SubjectId = "subject-1";

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore ClientStore { get; }
    private ResourceServerStore ResourceServerStore { get; }
    private ClientGrantStore ClientGrantStore { get; }
    private ConsentStore ConsentStore { get; }
    private GrantStore GrantStore { get; }

    public ClientStoreRelationalRemovalTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));
        Provider = serviceCollection.BuildServiceProvider();

        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        DbContext.Database.EnsureCreated();
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");

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
        ClientGrantStore = new ClientGrantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        ConsentStore = new ConsentStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        GrantStore = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
        Connection.Dispose();
    }

    #region Helpers

    private static PersistedSecret CreateSecret(string secretId) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = "encoded-value",
        }.Build();

    private static PersistedTenant CreateTenant() =>
        new PersistedTenantBuilder { TenantId = TenantId, DisplayName = "Tenant One" }.Build();

    private static PersistedResourceServer CreateResourceServer() =>
        new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
        }.Build();

    private static PersistedClient CreateClient(string clientId, bool withSecret) =>
        new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            Secrets = withSecret ? [CreateSecret($"{clientId}-secret")] : [],
        }.Build();

    private static PersistedClientGrant CreateClientGrant(string clientId) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = string.Empty,
            Scopes = [],
        };

    private static PersistedConsent CreateConsent(string clientId) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            SubjectId = SubjectId,
            RequestedScopes = ["openid"],
            GrantedScopes = ["openid"],
            CreatedWhen = DateTimeOffset.UnixEpoch,
            UpdatedWhen = DateTimeOffset.UnixEpoch,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedGrant CreateGrant(string clientId) =>
        new()
        {
            GrantType = "authorization_code",
            HashedKey = $"{clientId}-hashed",
            GrantId = $"{clientId}-grant",
            ConcurrencyToken = string.Empty,
            TenantId = TenantId,
            ClientId = clientId,
            SubjectId = SubjectId,
            SessionId = null,
            RefreshTokenLineageId = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = null,
            ConsumedWhen = null,
            ProtectedPayload = "protected-payload",
        };

    private async ValueTask SeedAsync(bool withSecrets, CancellationToken cancellationToken)
    {
        await TenantStore.AddAsync(CreateTenant(), cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        await ResourceServerStore.AddAsync(CreateResourceServer(), cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        foreach (var clientId in new[] { ClientOneId, ClientTwoId })
        {
            await ClientStore.AddAsync(CreateClient(clientId, withSecrets), cancellationToken);
            await DbContext.SaveChangesAsync(cancellationToken);

            await ClientGrantStore.AddAsync(CreateClientGrant(clientId), cancellationToken);
            await DbContext.SaveChangesAsync(cancellationToken);

            await ConsentStore.SaveAsync(CreateConsent(clientId), cancellationToken);
            await DbContext.SaveChangesAsync(cancellationToken);

            await GrantStore.AddAsync(CreateGrant(clientId), cancellationToken);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask AssertOnlyClientTwoRemainsAsync(
        bool withSecrets,
        CancellationToken cancellationToken
    )
    {
        DbContext.ChangeTracker.Clear();

        var client = Assert.Single(
            await DbContext.Clients.IgnoreQueryFilters().ToListAsync(cancellationToken)
        );
        Assert.Equal(ClientTwoId, client.ClientId);

        var clientSecrets = await DbContext
            .ClientSecrets.IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        var secrets = await DbContext.Secrets.IgnoreQueryFilters().ToListAsync(cancellationToken);
        if (withSecrets)
        {
            var clientSecret = Assert.Single(clientSecrets);
            Assert.Equal(client.Id, clientSecret.ClientId);

            var secret = Assert.Single(secrets);
            Assert.Equal($"{ClientTwoId}-secret", secret.SecretId);
        }
        else
        {
            Assert.Empty(clientSecrets);
            Assert.Empty(secrets);
        }

        var clientGrant = Assert.Single(
            await DbContext.ClientGrants.IgnoreQueryFilters().ToListAsync(cancellationToken)
        );
        Assert.Equal(client.Id, clientGrant.ClientId);

        var consent = Assert.Single(
            await DbContext.Consents.IgnoreQueryFilters().ToListAsync(cancellationToken)
        );
        Assert.Equal(client.Id, consent.ClientId);

        var grant = Assert.Single(
            await DbContext.Grants.IgnoreQueryFilters().ToListAsync(cancellationToken)
        );
        Assert.Equal(client.Id, grant.ClientId);
        Assert.Equal($"{ClientTwoId}-grant", grant.GrantId);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesTheClientWithEverythingItOwns()
    {
        await SeedAsync(withSecrets: true, CancellationToken.None);

        var removed = await ClientStore.RemoveAsync(ClientOneId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        await AssertOnlyClientTwoRemainsAsync(withSecrets: true, CancellationToken.None);
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_WhenTheClientHasNoSecrets_RemovesItsGrantsAndConsents()
    {
        await SeedAsync(withSecrets: false, CancellationToken.None);

        var removed = await ClientStore.RemoveAsync(ClientOneId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);
        await AssertOnlyClientTwoRemainsAsync(withSecrets: false, CancellationToken.None);
    }

    #endregion
}
