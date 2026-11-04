// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class PlatformStoreLifecycleTests : IDisposable
{
    private const string PlatformId = "server-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly PlatformStore _store;

    public PlatformStoreLifecycleTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _store = new PlatformStore(
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

    private static PersistedPlatform CreateServer() =>
        new()
        {
            PlatformId = PlatformId,
            ConcurrencyToken = string.Empty,
            DisplayName = "Test Server",
            Settings = new PersistedPlatformSettings
            {
                PlatformId = PlatformId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
        };

    private async Task SeedServerAsync()
    {
        await _store.AddAsync(CreateServer(), CancellationToken.None);
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

        var reloaded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("""{"flag":true}""", reloaded.ReconciledSettings?.GetRawText());
    }

    [Fact]
    public async Task GetOrDefaultAsync_RoundTripsDisplayName()
    {
        await SeedServerAsync();

        var reloaded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("Test Server", reloaded.DisplayName);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDisplayName()
    {
        await SeedServerAsync();

        var server = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(server);
        server.DisplayName = "Renamed Server";

        await _store.UpdateAsync(server, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("Renamed Server", reloaded.DisplayName);
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_RecordsTheFingerprintAndTheSettings()
    {
        await SeedServerAsync();

        var server = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(server);
        Assert.Null(server.ReconciledSettingsFingerprint);
        Assert.Null(server.ReconciledSettings);
        server.ReconciledSettingsFingerprint = "fingerprint-1";
        server.ReconciledSettings = JsonElement.Parse("""{"flag":true}""");

        await _store.UpdateReconciledSettingsAsync(server, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("fingerprint-1", reloaded.ReconciledSettingsFingerprint);
        Assert.Equal("""{"flag":true}""", reloaded.ReconciledSettings?.GetRawText());
        Assert.Equal("Test Server", reloaded.DisplayName);
    }

    [Fact]
    public async Task UpdateReconciledSettingsAsync_WhenTheServerChanged_Throws()
    {
        await SeedServerAsync();

        var server = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
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

        var seeded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(seeded);
        Assert.Equal("fingerprint-1", seeded.ReconciledSettingsFingerprint);
        _dbContext.ChangeTracker.Clear();

        var settings = await _store.GetSettingsOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(settings);
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
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

        var settings = await _store.GetSettingsOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(settings);
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
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

        var settings = await _store.GetSettingsOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(settings);
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(PlatformId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.ReconciledSettingsFingerprint);
        Assert.Null(reloaded.ReconciledSettings);
    }
}
