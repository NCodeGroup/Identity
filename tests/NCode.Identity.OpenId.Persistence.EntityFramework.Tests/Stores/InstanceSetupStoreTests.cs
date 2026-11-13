// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses SQLite because the EF InMemory provider does not enforce concurrency tokens, which keep two replicas from both
// completing setup.
public sealed class InstanceSetupStoreTests : IDisposable
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }

    public InstanceSetupStoreTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(Connection));
        Provider = serviceCollection.BuildServiceProvider();

        using var scope = Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<OpenIdDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        Provider.Dispose();
        Connection.Dispose();
    }

    #region Helpers

    private static InstanceSetupStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), context);

    private static PersistedInstanceSetup CreateInstanceSetup() =>
        new()
        {
            Key = PersistedInstanceSetup.SingletonKey,
            IsInitialized = true,
            TokenHash = Enumerable
                .Range(0, PersistedInstanceSetup.TokenHashLength)
                .Select(index => (byte)index)
                .ToArray(),
            TokenIssuedAt = Now.AddMinutes(-30),
            TokenExpiresAt = Now.AddMinutes(30),
            InitializedAt = Now,
            InitializedByPrincipalId = "principal-one",
            ConcurrencyToken = string.Empty,
        };

    private async ValueTask AddAsync(
        PersistedInstanceSetup instanceSetup,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context).AddAsync(instanceSetup, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask<PersistedInstanceSetup?> GetOrDefaultAsync(
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).GetOrDefaultAsync(cancellationToken);
    }

    #endregion

    #region AddAsync and GetOrDefaultAsync Tests

    [Fact]
    public async Task AddThenGet_RoundTripsEveryMember()
    {
        var expected = CreateInstanceSetup();
        await AddAsync(expected, CancellationToken.None);

        var actual = await GetOrDefaultAsync(CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(PersistedInstanceSetup.SingletonKey, actual.Key);
        Assert.True(actual.IsInitialized);
        Assert.Equal(expected.TokenHash, actual.TokenHash);
        Assert.Equal(expected.TokenIssuedAt, actual.TokenIssuedAt);
        Assert.Equal(expected.TokenExpiresAt, actual.TokenExpiresAt);
        Assert.Equal(expected.InitializedAt, actual.InitializedAt);
        Assert.Equal("principal-one", actual.InitializedByPrincipalId);
        Assert.Equal(expected.ConcurrencyToken, actual.ConcurrencyToken);
        Assert.False(string.IsNullOrEmpty(actual.ConcurrencyToken));
    }

    [Fact]
    public async Task Get_OnAnEmptyDatabase_ReturnsNull()
    {
        var actual = await GetOrDefaultAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Update_WithTheCurrentToken_ChangesTheRowAndRenewsTheToken()
    {
        await AddAsync(CreateInstanceSetup(), CancellationToken.None);
        var current = await GetOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(current);
        var originalToken = current.ConcurrencyToken;

        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            current.IsInitialized = false;
            current.TokenHash = null;
            current.TokenIssuedAt = null;
            current.TokenExpiresAt = null;
            current.InitializedAt = null;
            current.InitializedByPrincipalId = null;
            await CreateStore(context).UpdateAsync(current, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        var updated = await GetOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(updated);
        Assert.False(updated.IsInitialized);
        Assert.Null(updated.TokenHash);
        Assert.Null(updated.TokenIssuedAt);
        Assert.Null(updated.TokenExpiresAt);
        Assert.Null(updated.InitializedAt);
        Assert.Null(updated.InitializedByPrincipalId);
        Assert.NotEqual(originalToken, updated.ConcurrencyToken);
    }

    [Fact]
    public async Task Update_WithAStaleToken_ThrowsStoreConcurrencyException()
    {
        await AddAsync(CreateInstanceSetup(), CancellationToken.None);
        var current = await GetOrDefaultAsync(CancellationToken.None);
        Assert.NotNull(current);
        current.ConcurrencyToken = "stale";

        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateStore(context).UpdateAsync(current, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Update_OfARowThatIsGone_ThrowsInvalidOperationException()
    {
        var missing = CreateInstanceSetup();
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateStore(context).UpdateAsync(missing, CancellationToken.None)
        );
    }

    #endregion
}
