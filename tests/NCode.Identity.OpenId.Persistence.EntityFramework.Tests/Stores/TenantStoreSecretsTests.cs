// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStoreSecretsTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore Store { get; }

    public TenantStoreSecretsTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        Store = new TenantStore(
            Mock.Of<IStoreProvider>(),
            Provider.GetRequiredService<IIdGenerator<long>>(),
            DbContext
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedSecret CreateSecret(string secretId) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = "RS256",
            EncodedValue = "encoded-value",
        }.Build();

    private async Task SeedTenantAsync(params PersistedSecret[] secrets)
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder
        {
            TenantId = TenantId,
            DisplayName = "Tenant One",
            Secrets = secrets,
        }.Build();

        await Store.AddAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task<string> GetTenantSecretsTokenAsync()
    {
        var tenant = await DbContext
            .Tenants.AsNoTracking()
            .SingleAsync(x => x.TenantId == TenantId);
        return tenant.SecretsConcurrencyToken;
    }

    #region GetSecretOrDefaultAsync Tests

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenExists_ReturnsSecret()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var result = await Store.GetSecretOrDefaultAsync(
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

        var result = await Store.GetSecretOrDefaultAsync(
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
        await Store.AddSecretAsync(TenantId, added, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.False(string.IsNullOrEmpty(added.ConcurrencyToken));

        var reloaded = await Store.GetSecretOrDefaultAsync(
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
            await Store.AddSecretAsync(TenantId, CreateSecret("secret-1"), CancellationToken.None)
        );
    }

    #endregion

    #region UpdateSecretAsync Tests

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMatches_UpdatesMetadata()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var current = await Store.GetSecretOrDefaultAsync(
            TenantId,
            "secret-1",
            CancellationToken.None
        );
        Assert.NotNull(current);

        var updated = new PersistedSecretBuilder
        {
            SecretId = current.SecretId,
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
        }.Build();

        await Store.UpdateSecretAsync(TenantId, updated, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        var reloaded = await Store.GetSecretOrDefaultAsync(
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
        var staged = new PersistedSecretBuilder
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            SecretType = secret.SecretType,
            SecretEncoding = secret.SecretEncoding,
            Origin = secret.Origin,
            ManagedBy = "rotation",
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
        }.Build();

        await Store.AddSecretAsync(TenantId, staged, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await Store.GetSecretOrDefaultAsync(
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
            await Store.UpdateSecretAsync(TenantId, stale, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenMissing_Throws()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.UpdateSecretAsync(TenantId, CreateSecret("missing"), CancellationToken.None)
        );
    }

    #endregion

    #region RemoveSecretAsync Tests

    [Fact]
    public async Task RemoveSecretAsync_WhenExists_RemovesAndReturnsTrue()
    {
        await SeedTenantAsync(CreateSecret("secret-1"), CreateSecret("secret-2"));
        var tokenBefore = await GetTenantSecretsTokenAsync();

        var removed = await Store.RemoveSecretAsync(TenantId, "secret-1", CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);

        var gone = await Store.GetSecretOrDefaultAsync(
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

        var removed = await Store.RemoveSecretAsync(TenantId, "missing", CancellationToken.None);

        Assert.False(removed);
    }

    #endregion

    #region MarkSecretsChangedAsync Tests

    [Fact]
    public async Task MarkSecretsChangedAsync_WhenTheTenantExists_IssuesANewSecretsToken()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));
        var tokenBefore = await GetTenantSecretsTokenAsync();

        var marked = await Store.MarkSecretsChangedAsync(TenantId, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(marked);

        var tokenAfter = await GetTenantSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task MarkSecretsChangedAsync_WhenTheTenantIsMissing_ReturnsFalse()
    {
        await SeedTenantAsync(CreateSecret("secret-1"));

        var marked = await Store.MarkSecretsChangedAsync("missing", CancellationToken.None);

        Assert.False(marked);
    }

    #endregion
}
