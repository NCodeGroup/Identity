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

public sealed class TenantStoreSecretsTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;

    public TenantStoreSecretsTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _store = new TenantStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            _dbContext
        );
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

    private async Task SeedTenantAsync(params PersistedSecret[] secrets)
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
                Value = secrets,
            },
        };

        await _store.AddAsync(tenant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task<string> GetTenantSecretsTokenAsync()
    {
        var tenant = await _dbContext
            .Tenants.AsNoTracking()
            .SingleAsync(x => x.TenantId == TenantId);
        return tenant.SecretsConcurrencyToken;
    }

    #region GetSecretOrDefaultAsync Tests

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenExists_ReturnsSecret()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretOrDefaultAsync(
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal("secret-1", result.SecretId);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretOrDefaultAsync(
            TenantId,
            "missing",
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region AddSecretAsync Tests

    [Fact]
    public async Task AddSecretAsync_WhenNew_AssignsTokenAndBumpsTenantToken()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));
        var tokenBefore = await GetTenantSecretsTokenAsync();

        var added = CreateSecret("secret-2");
        await _store.AddSecretAsync(TenantId, added, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.False(string.IsNullOrEmpty(added.ConcurrencyToken));

        var reloaded = await _store.GetSecretOrDefaultAsync(
            TenantId,
            "secret-2",
            CancellationToken.None
        );
        Assert.NotNull(reloaded);

        var tokenAfter = await GetTenantSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task AddSecretAsync_WhenDuplicate_Throws()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.AddSecretAsync(TenantId, CreateSecret("secret-1"), CancellationToken.None)
        );
    }

    #endregion

    #region UpdateSecretAsync Tests

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMatches_UpdatesMetadata()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var current = await _store.GetSecretOrDefaultAsync(
            TenantId,
            "secret-1",
            CancellationToken.None
        );
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

        await _store.UpdateSecretAsync(TenantId, updated, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _store.GetSecretOrDefaultAsync(
            TenantId,
            "secret-1",
            CancellationToken.None
        );
        Assert.NotNull(reloaded);
        Assert.Equal("enc", reloaded.Use);
        Assert.Equal("RS512", reloaded.Algorithm);
        Assert.Equal(current.EncodedValue, reloaded.EncodedValue);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMismatch_Throws()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var stale = CreateSecret("secret-1");
        stale.ConcurrencyToken = "stale-token";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await _store.UpdateSecretAsync(TenantId, stale, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenMissing_Throws()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.UpdateSecretAsync(
                TenantId,
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
        await SeedTenantAsync(CreateSecret("secret-1"), CreateSecret("secret-2"));
        var tokenBefore = await GetTenantSecretsTokenAsync();

        var removed = await _store.RemoveSecretAsync(TenantId, "secret-1", CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);

        var gone = await _store.GetSecretOrDefaultAsync(
            TenantId,
            "secret-1",
            CancellationToken.None
        );
        Assert.Null(gone);

        var tokenAfter = await GetTenantSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task RemoveSecretAsync_WhenMissing_ReturnsFalse()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var removed = await _store.RemoveSecretAsync(TenantId, "missing", CancellationToken.None);

        Assert.False(removed);
    }

    #endregion
}
