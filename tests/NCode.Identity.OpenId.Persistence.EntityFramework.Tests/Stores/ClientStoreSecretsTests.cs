// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStoreSecretsTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore TenantStore { get; }
    private ClientStore Store { get; }

    public ClientStoreSecretsTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        Store = new ClientStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            DbContext,
            TestCryptoServices.CreateResourceIdGenerator()
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

    private async ValueTask SeedClientAsync(
        PersistedSecret[] secrets,
        CancellationToken cancellationToken
    )
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder
        {
            TenantId = TenantId,
            DisplayName = "Tenant One",
        }.Build();

        await TenantStore.AddAsync(tenant, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        var client = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = ClientId,
            Secrets = secrets,
        }.Build();

        await Store.AddAsync(client, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    private async ValueTask<PersistedSecret?> GetSecretOrDefaultAsync(
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var secrets = await Store.GetSecretsOrDefaultAsync(ClientId, cancellationToken);
        return secrets?.Value.SingleOrDefault(x =>
            string.Equals(x.SecretId, secretId, StringComparison.Ordinal)
        );
    }

    private async ValueTask<string> GetClientSecretsTokenAsync(CancellationToken cancellationToken)
    {
        var client = await DbContext
            .Clients.AsNoTracking()
            .SingleAsync(x => x.ClientId == ClientId, cancellationToken);
        return client.SecretsConcurrencyToken;
    }

    #region GetSecretsOrDefaultAsync Tests

    [Fact]
    public async Task GetSecretsOrDefaultAsync_WhenExists_ReturnsSecrets()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        var result = await Store.GetSecretsOrDefaultAsync(ClientId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Single(result.Value);
        Assert.Equal("secret-1", result.Value.Single().SecretId);
    }

    [Fact]
    public async Task GetSecretsOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        var result = await Store.GetSecretsOrDefaultAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region GetSecretOrDefaultAsync Tests

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenExists_ReturnsSecretWithOwningTenant()
    {
        await SeedClientAsync(
            [CreateSecret("secret-1"), CreateSecret("secret-2")],
            CancellationToken.None
        );

        var result = await Store.GetSecretOrDefaultAsync(
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
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        var result = await Store.GetSecretOrDefaultAsync(
            ClientId,
            "missing",
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenClientMissing_ReturnsNull()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        var result = await Store.GetSecretOrDefaultAsync(
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
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);
        var tokenBefore = await GetClientSecretsTokenAsync(CancellationToken.None);

        var added = CreateSecret("secret-2");
        await Store.AddSecretAsync(ClientId, added, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.False(string.IsNullOrEmpty(added.ConcurrencyToken));

        var reloaded = await GetSecretOrDefaultAsync("secret-2", CancellationToken.None);
        Assert.NotNull(reloaded);

        var tokenAfter = await GetClientSecretsTokenAsync(CancellationToken.None);
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task AddSecretAsync_WhenDuplicate_Throws()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.AddSecretAsync(ClientId, CreateSecret("secret-1"), CancellationToken.None)
        );
    }

    [Fact]
    public async Task AddSecretAsync_WhenKeyIdIsSet_RoundTripsKeyId()
    {
        await SeedClientAsync([], CancellationToken.None);

        var added = new PersistedSecretBuilder
        {
            SecretId = "secret-2",
            KeyId = "client-kid",
            Use = "sig",
            Algorithm = "RS256",
            SecretType = SecretTypes.Rsa,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Spki,
            KeySizeBits = 2048,
            EncodedValue = "encoded-value",
        }.Build();
        await Store.AddSecretAsync(ClientId, added, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await GetSecretOrDefaultAsync("secret-2", CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("client-kid", reloaded.KeyId);
    }

    #endregion

    #region UpdateSecretAsync Tests

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMatches_UpdatesMetadata()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        var current = await GetSecretOrDefaultAsync("secret-1", CancellationToken.None);
        Assert.NotNull(current);

        var updated = new PersistedSecretBuilder
        {
            SecretId = current.SecretId,
            ConcurrencyToken = current.ConcurrencyToken,
            Use = "enc",
            Algorithm = "RS512",
            CreatedWhen = current.CreatedWhen,
            ExpiresWhen = current.ExpiresWhen.AddYears(1),
            ActivatesWhen = current.CreatedWhen,
            RetiresWhen = current.ExpiresWhen.AddYears(1),
            SecretType = current.SecretType,
            SecretEncoding = current.SecretEncoding,
            Origin = current.Origin,
            KeySizeBits = current.KeySizeBits,
            EncodedValue = current.EncodedValue,
        }.Build();

        await Store.UpdateSecretAsync(ClientId, updated, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        var reloaded = await GetSecretOrDefaultAsync("secret-1", CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("enc", reloaded.Use);
        Assert.Equal("RS512", reloaded.Algorithm);
        Assert.Equal(current.EncodedValue, reloaded.EncodedValue);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMismatch_Throws()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        var stale = CreateSecret("secret-1");
        stale.ConcurrencyToken = "stale-token";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Store.UpdateSecretAsync(ClientId, stale, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenMissing_Throws()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.UpdateSecretAsync(ClientId, CreateSecret("missing"), CancellationToken.None)
        );
    }

    #endregion

    #region RemoveSecretAsync Tests

    [Fact]
    public async Task RemoveSecretAsync_WhenExists_RemovesAndReturnsTrue()
    {
        await SeedClientAsync(
            [CreateSecret("secret-1"), CreateSecret("secret-2")],
            CancellationToken.None
        );
        var tokenBefore = await GetClientSecretsTokenAsync(CancellationToken.None);

        var removed = await Store.RemoveSecretAsync(ClientId, "secret-1", CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Assert.True(removed);

        var gone = await GetSecretOrDefaultAsync("secret-1", CancellationToken.None);
        Assert.Null(gone);

        var tokenAfter = await GetClientSecretsTokenAsync(CancellationToken.None);
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task RemoveSecretAsync_WhenMissing_ReturnsFalse()
    {
        await SeedClientAsync([CreateSecret("secret-1")], CancellationToken.None);

        var removed = await Store.RemoveSecretAsync(ClientId, "missing", CancellationToken.None);

        Assert.False(removed);
    }

    #endregion
}
