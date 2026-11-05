// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses SQLite because the EF InMemory provider does not enforce the unique index that keeps the platform to one row.
public sealed class PlatformStoreLifecycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly PlatformStore _store;

    public PlatformStoreLifecycleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _dbContext.Database.EnsureCreated();
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
        _connection.Dispose();
    }

    private static PersistedPlatform CreatePlatform() =>
        new()
        {
            ConcurrencyToken = string.Empty,
            Settings = new PersistedPlatformSettings
            {
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
        };

    private async Task SeedPlatformAsync()
    {
        await _store.AddAsync(CreatePlatform(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        var platform = CreatePlatform();

        await _store.AddAsync(platform, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(platform.ConcurrencyToken));
        Assert.False(string.IsNullOrEmpty(platform.Settings.ConcurrencyToken));
    }

    [Fact]
    public async Task AddAsync_WhenARowExists_ThrowsOnSave()
    {
        await SeedPlatformAsync();

        await _store.AddAsync(CreatePlatform(), CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await _dbContext.SaveChangesAsync()
        );
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenNoRowExists_ReturnsNull()
    {
        var platform = await _store.GetOrDefaultAsync(CancellationToken.None);

        Assert.Null(platform);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenARowExists_RoundTripsTheSettings()
    {
        await SeedPlatformAsync();

        var platform = await _store.GetOrDefaultAsync(CancellationToken.None);

        Assert.NotNull(platform);
        Assert.Equal(OpenIdResourceIds.Platform, platform.ResourceId);
        Assert.Equal(OpenIdResourceIds.Platform, platform.Settings.ResourceId);
        Assert.Equal("{}", platform.Settings.Value.GetRawText());
    }

    [Fact]
    public async Task GetSettingsOrDefaultAsync_WhenNoRowExists_ReturnsNull()
    {
        var settings = await _store.GetSettingsOrDefaultAsync(CancellationToken.None);

        Assert.Null(settings);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheRowChanged_Throws()
    {
        await SeedPlatformAsync();

        var platform = await _store.GetOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(platform);
        platform.ConcurrencyToken = "stale";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateAsync(platform, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesTheSettingsAndTheirToken()
    {
        await SeedPlatformAsync();

        var settings = await _store.GetSettingsOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(settings);
        var previousToken = settings.ConcurrencyToken;
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetSettingsOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("""{"key":"value"}""", reloaded.Value.GetRawText());
        Assert.NotEqual(previousToken, reloaded.ConcurrencyToken);
        Assert.Equal(settings.ConcurrencyToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheSettingsChanged_Throws()
    {
        await SeedPlatformAsync();

        var settings = await _store.GetSettingsOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(settings);
        settings.ConcurrencyToken = "stale";

        var exception = await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateSettingsAsync(settings, CancellationToken.None)
        );
        Assert.Equal(
            "The platform's settings have been modified by another process. Please reload and try again.",
            exception.Message
        );
    }
}
