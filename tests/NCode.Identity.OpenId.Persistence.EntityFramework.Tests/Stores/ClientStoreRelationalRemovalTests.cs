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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Validates ClientStore.RemoveAsync against a relational provider that enforces foreign keys (ADR-0212). The EF
// InMemory provider skips foreign keys, so the delete order and the exact set of rows removed are proven here.
public sealed class ClientStoreRelationalRemovalTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";
    private const string ClientOneId = "client-1";
    private const string ClientTwoId = "client-2";
    private const string SubjectId = "subject-1";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _clientStore;
    private readonly ResourceServerStore _resourceServerStore;
    private readonly ClientGrantStore _clientGrantStore;
    private readonly ConsentStore _consentStore;
    private readonly GrantStore _grantStore;

    public ClientStoreRelationalRemovalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));
        _provider = serviceCollection.BuildServiceProvider();

        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _dbContext.Database.EnsureCreated();

        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _clientStore = new ClientStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _resourceServerStore = new ResourceServerStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
        _clientGrantStore = new ClientGrantStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
        _consentStore = new ConsentStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _grantStore = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    #region Helpers

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = string.Empty,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            KeySizeBits = 256,
            EncodedValue = "encoded-value",
        };

    private static PersistedTenant CreateTenant() =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = "Tenant One",
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

    private static PersistedResourceServer CreateResourceServer() =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Identifier = "https://api.example.com",
            ConcurrencyToken = string.Empty,
            Name = "Example API",
            IsSystem = false,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = TenantId,
                ResourceServerId = ResourceServerId,
                Value = EmptyObject(),
            },
            Scopes = [],
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = TenantId,
                ResourceServerId = ResourceServerId,
                Value = [],
            },
        };

    private static PersistedClient CreateClient(string clientId, bool withSecret) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
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
                Value = withSecret ? [CreateSecret($"{clientId}-secret")] : [],
            },
        };

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

    private async Task SeedAsync(bool withSecrets)
    {
        await _tenantStore.AddAsync(CreateTenant(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        await _resourceServerStore.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        foreach (var clientId in new[] { ClientOneId, ClientTwoId })
        {
            await _clientStore.AddAsync(
                CreateClient(clientId, withSecrets),
                CancellationToken.None
            );
            await _dbContext.SaveChangesAsync();

            await _clientGrantStore.AddAsync(CreateClientGrant(clientId), CancellationToken.None);
            await _dbContext.SaveChangesAsync();

            await _consentStore.SaveAsync(CreateConsent(clientId), CancellationToken.None);
            await _dbContext.SaveChangesAsync();

            await _grantStore.AddAsync(CreateGrant(clientId), CancellationToken.None);
            await _dbContext.SaveChangesAsync();
        }

        _dbContext.ChangeTracker.Clear();
    }

    private async Task AssertOnlyClientTwoRemainsAsync(bool withSecrets)
    {
        _dbContext.ChangeTracker.Clear();

        var client = Assert.Single(await _dbContext.Clients.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(ClientTwoId, client.ClientId);

        var clientSecrets = await _dbContext.ClientSecrets.IgnoreQueryFilters().ToListAsync();
        var secrets = await _dbContext.Secrets.IgnoreQueryFilters().ToListAsync();
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
            await _dbContext.ClientGrants.IgnoreQueryFilters().ToListAsync()
        );
        Assert.Equal(client.Id, clientGrant.ClientId);

        var consent = Assert.Single(await _dbContext.Consents.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(client.Id, consent.ClientId);

        var grant = Assert.Single(await _dbContext.Grants.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(client.Id, grant.ClientId);
        Assert.Equal($"{ClientTwoId}-grant", grant.GrantId);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_RemovesTheClientWithEverythingItOwns()
    {
        await SeedAsync(withSecrets: true);

        var removed = await _clientStore.RemoveAsync(ClientOneId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        await AssertOnlyClientTwoRemainsAsync(withSecrets: true);
    }

    [Fact]
    public async Task RemoveAsync_OnARelationalProvider_WhenTheClientHasNoSecrets_RemovesItsGrantsAndConsents()
    {
        await SeedAsync(withSecrets: false);

        var removed = await _clientStore.RemoveAsync(ClientOneId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        await AssertOnlyClientTwoRemainsAsync(withSecrets: false);
    }

    #endregion
}
