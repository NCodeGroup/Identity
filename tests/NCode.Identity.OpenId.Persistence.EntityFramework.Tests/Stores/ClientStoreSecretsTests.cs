#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStoreSecretsTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _store;

    public ClientStoreSecretsTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new ClientStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

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
            KeySizeBits = 256,
            EncodedValue = "encoded-value",
        };

    private async Task SeedClientAsync(params PersistedSecret[] secrets)
    {
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            DomainName = null,
            IsDisabled = false,
            DisplayName = "Tenant One",
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

        var client = new PersistedClient
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

        await _store.AddAsync(client, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task<PersistedSecret?> GetSecretOrDefaultAsync(string secretId)
    {
        var secrets = await _store.GetSecretsOrDefaultAsync(ClientId, CancellationToken.None);
        return secrets?.Value.SingleOrDefault(x =>
            string.Equals(x.SecretId, secretId, StringComparison.Ordinal)
        );
    }

    private async Task<string> GetClientSecretsTokenAsync()
    {
        var client = await _dbContext
            .Clients.AsNoTracking()
            .SingleAsync(x => x.ClientId == ClientId);
        return client.SecretsConcurrencyToken;
    }

    #region GetSecretsOrDefaultAsync Tests

    [Fact]
    public async Task GetSecretsOrDefaultAsync_WhenExists_ReturnsSecrets()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretsOrDefaultAsync(ClientId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Single(result.Value);
        Assert.Equal("secret-1", result.Value.Single().SecretId);
    }

    [Fact]
    public async Task GetSecretsOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretsOrDefaultAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region GetSecretOrDefaultAsync Tests

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenExists_ReturnsSecretWithOwningTenant()
    {
        await SeedClientAsync(CreateSecret("secret-1"), CreateSecret("secret-2"));

        var result = await _store.GetSecretOrDefaultAsync(
            ClientId,
            "secret-2",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Equal("secret-2", result.Value.SecretId);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenSecretMissing_ReturnsNull()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretOrDefaultAsync(
            ClientId,
            "missing",
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenClientMissing_ReturnsNull()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretOrDefaultAsync(
            "missing",
            "secret-1",
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region AddSecretAsync Tests

    [Fact]
    public async Task AddSecretAsync_WhenNew_AssignsTokenAndBumpsClientToken()
    {
        await SeedClientAsync(CreateSecret("secret-1"));
        var tokenBefore = await GetClientSecretsTokenAsync();

        var added = CreateSecret("secret-2");
        await _store.AddSecretAsync(ClientId, added, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.False(string.IsNullOrEmpty(added.ConcurrencyToken));

        var reloaded = await GetSecretOrDefaultAsync("secret-2");
        Assert.NotNull(reloaded);

        var tokenAfter = await GetClientSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task AddSecretAsync_WhenDuplicate_Throws()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.AddSecretAsync(ClientId, CreateSecret("secret-1"), CancellationToken.None)
        );
    }

    #endregion

    #region UpdateSecretAsync Tests

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMatches_UpdatesMetadata()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var current = await GetSecretOrDefaultAsync("secret-1");
        Assert.NotNull(current);

        var updated = new PersistedSecret
        {
            SecretId = "secret-1",
            ConcurrencyToken = current.ConcurrencyToken,
            Use = "enc",
            Algorithm = "RS512",
            CreatedWhen = current.CreatedWhen,
            ExpiresWhen = current.ExpiresWhen.AddYears(1),
            SecretType = current.SecretType,
            KeySizeBits = current.KeySizeBits,
            EncodedValue = current.EncodedValue,
        };

        await _store.UpdateSecretAsync(ClientId, updated, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await GetSecretOrDefaultAsync("secret-1");
        Assert.NotNull(reloaded);
        Assert.Equal("enc", reloaded.Use);
        Assert.Equal("RS512", reloaded.Algorithm);
        Assert.Equal(current.EncodedValue, reloaded.EncodedValue);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMismatch_Throws()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var stale = CreateSecret("secret-1");
        stale.ConcurrencyToken = "stale-token";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await _store.UpdateSecretAsync(ClientId, stale, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenMissing_Throws()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.UpdateSecretAsync(
                ClientId,
                CreateSecret("missing"),
                CancellationToken.None
            )
        );
    }

    #endregion

    #region RemoveSecretAsync Tests

    [Fact]
    public async Task RemoveSecretAsync_WhenExists_RemovesAndReturnsTrue()
    {
        await SeedClientAsync(CreateSecret("secret-1"), CreateSecret("secret-2"));
        var tokenBefore = await GetClientSecretsTokenAsync();

        var removed = await _store.RemoveSecretAsync(ClientId, "secret-1", CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);

        var gone = await GetSecretOrDefaultAsync("secret-1");
        Assert.Null(gone);

        var tokenAfter = await GetClientSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task RemoveSecretAsync_WhenMissing_ReturnsFalse()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var removed = await _store.RemoveSecretAsync(ClientId, "missing", CancellationToken.None);

        Assert.False(removed);
    }

    #endregion
}
