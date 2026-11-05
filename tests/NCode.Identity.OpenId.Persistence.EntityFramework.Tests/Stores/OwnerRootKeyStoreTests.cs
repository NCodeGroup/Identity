// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses SQLite because the EF InMemory provider does not enforce unique indexes or concurrency tokens, which keep two
// replicas from storing two root keys for one owner (ADR-1414).
public sealed class OwnerRootKeyStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public OwnerRootKeyStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options => options.UseSqlite(_connection));
        _provider = serviceCollection.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<OpenIdDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    #region Helpers

    private OwnerRootKeyStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), _provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static PersistedOwnerRootKey CreateRootKey(
        string ownerId,
        string state = OwnerRootKeyStates.Active,
        DateTimeOffset? validatedWhen = null,
        DateTimeOffset? failedWhen = null
    ) =>
        new()
        {
            OwnerType = SecretOwnerTypes.Tenant,
            OwnerId = ownerId,
            KeyWrapperName = "aws-kms",
            KeyReference = $"arn:aws:kms:us-west-2:111122223333:key/{ownerId}",
            State = state,
            CreatedWhen = Now.AddDays(-1),
            StateChangedWhen = Now.AddHours(-3),
            ValidatedWhen = validatedWhen,
            FailedWhen = failedWhen,
            Failure = failedWhen is null ? null : "AccessDenied: revoked",
        };

    private async Task AddAsync(params PersistedOwnerRootKey[] rootKeys)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);
        foreach (var rootKey in rootKeys)
            await store.AddAsync(rootKey, CancellationToken.None);

        await context.SaveChangesAsync();
    }

    private async Task<PersistedOwnerRootKey?> FindAsync(SecretOwner owner)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).FindAsync(owner, CancellationToken.None);
    }

    #endregion

    #region AddAsync and FindAsync Tests

    [Fact]
    public async Task AddThenFind_RoundTripsEveryMember()
    {
        var expected = CreateRootKey("Tenant-One", validatedWhen: Now.AddMinutes(-5));
        expected.FailedWhen = Now.AddMinutes(-65);
        expected.Failure = "earlier failure";
        await AddAsync(expected);

        var actual = await FindAsync(SecretOwner.ForTenant("Tenant-One"));

        Assert.NotNull(actual);
        Assert.Equal(SecretOwnerTypes.Tenant, actual.OwnerType);
        Assert.Equal("Tenant-One", actual.OwnerId);
        Assert.Equal(expected.KeyWrapperName, actual.KeyWrapperName);
        Assert.Equal(expected.KeyReference, actual.KeyReference);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.CreatedWhen, actual.CreatedWhen);
        Assert.Equal(expected.StateChangedWhen, actual.StateChangedWhen);
        Assert.Equal(expected.ValidatedWhen, actual.ValidatedWhen);
        Assert.Equal(expected.FailedWhen, actual.FailedWhen);
        Assert.Equal("earlier failure", actual.Failure);
        Assert.Equal(expected.ConcurrencyToken, actual.ConcurrencyToken);
        Assert.False(string.IsNullOrEmpty(actual.ConcurrencyToken));
    }

    [Fact]
    public async Task Find_MatchesTheOwnerWithoutRegardToCase()
    {
        await AddAsync(CreateRootKey("Tenant-One"));

        Assert.NotNull(await FindAsync(new SecretOwner("TENANT", "tenant-one")));
        Assert.Null(await FindAsync(SecretOwner.ForTenant("tenant-two")));
        Assert.Null(await FindAsync(SecretOwner.ForServer("tenant-one")));
    }

    [Fact]
    public async Task Add_SecondRootKeyForTheSameOwner_ViolatesTheUniqueIndex()
    {
        await AddAsync(CreateRootKey("Tenant-One"));

        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            await AddAsync(CreateRootKey("TENANT-ONE"))
        );
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Update_WithTheCurrentToken_ChangesTheRowAndRenewsTheToken()
    {
        await AddAsync(CreateRootKey("Tenant-One"));
        var current = await FindAsync(SecretOwner.ForTenant("tenant-one"));
        Assert.NotNull(current);
        var originalToken = current.ConcurrencyToken;

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            current.State = OwnerRootKeyStates.Unavailable;
            current.FailedWhen = Now;
            current.Failure = "AccessDenied: revoked";
            await CreateStore(context).UpdateAsync(current, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        var updated = await FindAsync(SecretOwner.ForTenant("tenant-one"));
        Assert.Equal(OwnerRootKeyStates.Unavailable, updated?.State);
        Assert.Equal(Now, updated?.FailedWhen);
        Assert.Equal("AccessDenied: revoked", updated?.Failure);
        Assert.NotEqual(originalToken, updated?.ConcurrencyToken);
    }

    [Fact]
    public async Task Update_WithAStaleToken_ThrowsStoreConcurrencyException()
    {
        await AddAsync(CreateRootKey("Tenant-One"));
        var current = await FindAsync(SecretOwner.ForTenant("tenant-one"));
        Assert.NotNull(current);
        current.ConcurrencyToken = "stale";

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateStore(context).UpdateAsync(current, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Update_OfARowThatIsGone_ThrowsInvalidOperationException()
    {
        var missing = CreateRootKey("Tenant-One");
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateStore(context).UpdateAsync(missing, CancellationToken.None)
        );
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task Remove_WithTheCurrentToken_RemovesTheRow()
    {
        await AddAsync(CreateRootKey("Tenant-One"), CreateRootKey("tenant-two"));
        var current = await FindAsync(SecretOwner.ForTenant("tenant-one"));
        Assert.NotNull(current);

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var removed = await CreateStore(context).RemoveAsync(current, CancellationToken.None);
            await context.SaveChangesAsync();
            Assert.True(removed);
        }

        Assert.Null(await FindAsync(SecretOwner.ForTenant("tenant-one")));
        Assert.NotNull(await FindAsync(SecretOwner.ForTenant("tenant-two")));
    }

    [Fact]
    public async Task Remove_OfARowThatIsGone_ReturnsFalse()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        var removed = await CreateStore(context)
            .RemoveAsync(CreateRootKey("Tenant-One"), CancellationToken.None);

        Assert.False(removed);
    }

    [Fact]
    public async Task Remove_WithAStaleToken_ThrowsStoreConcurrencyException()
    {
        await AddAsync(CreateRootKey("Tenant-One"));
        var current = await FindAsync(SecretOwner.ForTenant("tenant-one"));
        Assert.NotNull(current);
        current.ConcurrencyToken = "stale";
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateStore(context).RemoveAsync(current, CancellationToken.None)
        );
    }

    #endregion
}
