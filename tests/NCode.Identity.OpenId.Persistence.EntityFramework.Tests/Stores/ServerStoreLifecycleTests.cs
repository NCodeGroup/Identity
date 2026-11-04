// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ServerStoreLifecycleTests : IDisposable
{
    private const string ServerId = "server-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly ServerStore _store;
    private readonly MasterKeyStore _masterKeyStore;

    public ServerStoreLifecycleTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _store = new ServerStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            _dbContext
        );
        _masterKeyStore = new MasterKeyStore(
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

    private static PersistedMasterKey CreateMasterKey(
        string ownerType,
        string ownerId,
        int version
    ) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            Version = version,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            RetiredWhen = null,
            RootKeyProvider = KeyWrapperNames.Local,
            RootKeyId = $"root-key-{version}",
            RootWrappedKey = $"root-wrapped-key-{version}",
            RecoveryKeyId = null,
            RecoveryWrappedKey = null,
            KeyCheck = $"key-check-{version}",
        };

    private async Task SeedMasterKeysAsync(params PersistedMasterKey[] masterKeys)
    {
        foreach (var masterKey in masterKeys)
            await _masterKeyStore.AddAsync(masterKey, CancellationToken.None);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private static PersistedServer CreateServer(params PersistedSecret[] secrets) =>
        new()
        {
            ServerId = ServerId,
            ConcurrencyToken = string.Empty,
            DisplayName = "Test Server",
            Settings = new PersistedServerSettings
            {
                ServerId = ServerId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedServerSecrets
            {
                ServerId = ServerId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

    private async Task SeedServerAsync(params PersistedSecret[] secrets)
    {
        await _store.AddAsync(CreateServer(secrets), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        var server = CreateServer();

        await _store.AddAsync(server, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(server.ConcurrencyToken));
    }

    [Fact]
    public async Task AddAsync_RoundTripsTheReconciledSettings()
    {
        var server = CreateServer();
        server.ReconciledSettings = JsonElement.Parse("""{"flag":true}""");

        await _store.AddAsync(server, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("""{"flag":true}""", reloaded.ReconciledSettings?.GetRawText());
    }

    [Fact]
    public async Task RemoveAsync_ServerWithMasterKeys_RemovesThemAndKeepsOthers()
    {
        await SeedServerAsync();
        var otherServerKey = CreateMasterKey(SecretOwnerTypes.Server, "server-2", 1);
        var tenantKey = CreateMasterKey(SecretOwnerTypes.Tenant, ServerId, 1);
        await SeedMasterKeysAsync(
            CreateMasterKey(SecretOwnerTypes.Server, ServerId, 1),
            CreateMasterKey(SecretOwnerTypes.Server, ServerId.ToUpperInvariant(), 2),
            otherServerKey,
            tenantKey
        );

        var removed = await _store.RemoveAsync(ServerId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var remainingKeyIds = await _dbContext
            .MasterKeys.AsNoTracking()
            .Select(entity => entity.KeyId)
            .ToListAsync();
        Assert.Equal(2, remainingKeyIds.Count);
        Assert.Contains(otherServerKey.KeyId, remainingKeyIds);
        Assert.Contains(tenantKey.KeyId, remainingKeyIds);
    }

    [Fact]
    public async Task RemoveAsync_WhenNoSecrets_RemovesAndReturnsTrue()
    {
        await SeedServerAsync();

        var removed = await _store.RemoveAsync(ServerId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenHasSecrets_Throws()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(ServerId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        var removed = await _store.RemoveAsync("missing", CancellationToken.None);

        Assert.False(removed);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoSecrets_ReturnsFalse()
    {
        await SeedServerAsync();

        var result = await _store.HasDependentsAsync(ServerId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenHasSecrets_ReturnsTrue()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        var result = await _store.HasDependentsAsync(ServerId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task GetOrDefaultAsync_RoundTripsDisplayName()
    {
        await SeedServerAsync();

        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("Test Server", reloaded.DisplayName);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDisplayName()
    {
        await SeedServerAsync();

        var server = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(server);
        server.DisplayName = "Renamed Server";

        await _store.UpdateAsync(server, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("Renamed Server", reloaded.DisplayName);
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_RecordsTheFingerprintAndTheSettings()
    {
        await SeedServerAsync();

        var server = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(server);
        Assert.Null(server.ReconciledSettingsFingerprint);
        Assert.Null(server.ReconciledSettings);
        server.ReconciledSettingsFingerprint = "fingerprint-1";
        server.ReconciledSettings = JsonElement.Parse("""{"flag":true}""");

        await _store.UpdateReconciledSettingsAsync(server, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("fingerprint-1", reloaded.ReconciledSettingsFingerprint);
        Assert.Equal("""{"flag":true}""", reloaded.ReconciledSettings?.GetRawText());
        Assert.Equal("Test Server", reloaded.DisplayName);
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_WhenTheServerChanged_Throws()
    {
        await SeedServerAsync();

        var server = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(server);
        server.ConcurrencyToken = "stale";
        server.ReconciledSettingsFingerprint = "fingerprint-1";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateReconciledSettingsAsync(server, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_ClearsTheReconciledSettingsFingerprint()
    {
        var seed = CreateServer();
        seed.ReconciledSettingsFingerprint = "fingerprint-1";
        await _store.AddAsync(seed, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var seeded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(seeded);
        Assert.Equal("fingerprint-1", seeded.ReconciledSettingsFingerprint);
        _dbContext.ChangeTracker.Clear();

        var settings = await _store.GetSettingsOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(settings);
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.ReconciledSettingsFingerprint);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheSettingsWereReconciled_KeepsTheReconciledSettings()
    {
        var seed = CreateServer();
        seed.ReconciledSettingsFingerprint = "fingerprint-1";
        seed.ReconciledSettings = JsonElement.Parse("""{"flag":true}""");
        await _store.AddAsync(seed, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var settings = await _store.GetSettingsOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(settings);
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.ReconciledSettingsFingerprint);
        Assert.Equal("""{"flag":true}""", reloaded.ReconciledSettings?.GetRawText());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAnEarlierChangeWasNotReconciled_ClearsTheReconciledSettings()
    {
        var seed = CreateServer();
        seed.ReconciledSettings = JsonElement.Parse("""{"flag":true}""");
        await _store.AddAsync(seed, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var settings = await _store.GetSettingsOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(settings);
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.ReconciledSettingsFingerprint);
        Assert.Null(reloaded.ReconciledSettings);
    }
}
