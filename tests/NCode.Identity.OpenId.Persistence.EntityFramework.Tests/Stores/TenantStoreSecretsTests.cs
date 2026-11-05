// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
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
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            Origin = SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = "encoded-value",
        };

    private async Task SeedTenantAsync(params PersistedSecret[] secrets)
    {
        _dbContext.SeedTeam(_provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            TeamId = "platform",
            ConcurrencyToken = string.Empty,
            Issuer = null,
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
            ActivatesWhen = current.CreatedWhen.AddDays(2),
            RetiresWhen = current.ExpiresWhen.AddMonths(6),
            ExpiresWhen = current.ExpiresWhen.AddYears(1),
            SecretType = current.SecretType,
            SecretEncoding = current.SecretEncoding,
            Origin = current.Origin,
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
        Assert.Equal(current.CreatedWhen.AddDays(2), reloaded.ActivatesWhen);
        Assert.Equal(current.ExpiresWhen.AddMonths(6), reloaded.RetiresWhen);
        Assert.Equal(current.ExpiresWhen.AddYears(1), reloaded.ExpiresWhen);
        Assert.Equal(current.EncodedValue, reloaded.EncodedValue);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task AddSecretAsync_StoresTheInstantsOfTheSecretsLifecycleAndItsManager()
    {
        await SeedTenantAsync();
        var secret = CreateSecret("secret-1");
        var staged = new PersistedSecret
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            SecretType = secret.SecretType,
            SecretEncoding = secret.SecretEncoding,
            Origin = secret.Origin,
            ManagedBy = "rotation",
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
        };

        await _store.AddSecretAsync(TenantId, staged, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetSecretOrDefaultAsync(
            TenantId,
            "secret-1",
            CancellationToken.None
        );
        Assert.NotNull(reloaded);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), reloaded.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(92), reloaded.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(99), reloaded.ExpiresWhen);
        Assert.Equal("rotation", reloaded.ManagedBy);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMismatch_Throws()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var stale = CreateSecret("secret-1");
        stale.ConcurrencyToken = "stale-token";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
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
