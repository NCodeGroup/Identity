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
    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private PlatformStore Store { get; }

    public PlatformStoreLifecycleTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        DbContext.Database.EnsureCreated();
        Store = new PlatformStore(
            Mock.Of<IStoreProvider>(),
            Provider.GetRequiredService<IIdGenerator<long>>(),
            DbContext
        );
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
        Connection.Dispose();
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

    private async ValueTask SeedPlatformAsync(CancellationToken cancellationToken)
    {
        await Store.AddAsync(CreatePlatform(), cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        var platform = CreatePlatform();

        await Store.AddAsync(platform, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(platform.ConcurrencyToken));
        Assert.False(string.IsNullOrEmpty(platform.Settings.ConcurrencyToken));
    }

    [Fact]
    public async Task AddAsync_WhenARowExists_ThrowsOnSave()
    {
        await SeedPlatformAsync(CancellationToken.None);

        await Store.AddAsync(CreatePlatform(), CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(async () => await DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenNoRowExists_ReturnsNull()
    {
        var platform = await Store.GetOrDefaultAsync(CancellationToken.None);

        Assert.Null(platform);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenARowExists_RoundTripsTheSettings()
    {
        await SeedPlatformAsync(CancellationToken.None);

        var platform = await Store.GetOrDefaultAsync(CancellationToken.None);

        Assert.NotNull(platform);
        Assert.Equal(OpenIdResourceIds.Platform, platform.ResourceId);
        Assert.Equal(OpenIdResourceIds.Platform, platform.Settings.ResourceId);
        Assert.Equal("{}", platform.Settings.Value.GetRawText());
    }

    [Fact]
    public async Task GetSettingsOrDefaultAsync_WhenNoRowExists_ReturnsNull()
    {
        var settings = await Store.GetSettingsOrDefaultAsync(CancellationToken.None);

        Assert.Null(settings);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheRowChanged_Throws()
    {
        await SeedPlatformAsync(CancellationToken.None);

        var platform = await Store.GetOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(platform);
        platform.ConcurrencyToken = "stale";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Store.UpdateAsync(platform, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesTheSettingsAndTheirToken()
    {
        await SeedPlatformAsync(CancellationToken.None);

        var settings = await Store.GetSettingsOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(settings);
        var previousToken = settings.ConcurrencyToken;
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["key"] = "value" }
        );

        await Store.UpdateSettingsAsync(settings, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await Store.GetSettingsOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("""{"key":"value"}""", reloaded.Value.GetRawText());
        Assert.NotEqual(previousToken, reloaded.ConcurrencyToken);
        Assert.Equal(settings.ConcurrencyToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheSettingsChanged_Throws()
    {
        await SeedPlatformAsync(CancellationToken.None);

        var settings = await Store.GetSettingsOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(settings);
        settings.ConcurrencyToken = "stale";

        var exception = await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Store.UpdateSettingsAsync(settings, CancellationToken.None)
        );
        Assert.Equal(
            "The platform's settings have been modified by another process. Please reload and try again.",
            exception.Message
        );
    }
}
