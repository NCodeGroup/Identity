// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses SQLite because the EF InMemory provider does not enforce unique indexes or concurrency tokens, which keep two
// replicas from creating the same version of an owner's master key (ADR-1235).
public sealed class MasterKeyStoreTests : IDisposable
{
    private const string TenantOneId = "Tenant-One";
    private const string TenantTwoId = "tenant-two";

    private static readonly DateTimeOffset CreatedWhen = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public MasterKeyStoreTests()
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

    private MasterKeyStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), _provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static PersistedMasterKey CreateMasterKey(
        string ownerType,
        string ownerId,
        int version,
        bool retired = false,
        DateTimeOffset? createdWhen = null
    ) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            Version = version,
            CreatedWhen = createdWhen ?? CreatedWhen,
            RetiredWhen = retired ? CreatedWhen.AddDays(30) : null,
            RootKeyProvider = KeyWrapperNames.Local,
            RootKeyId = $"root-key-{version}",
            RootWrappedKey = $"root-wrapped-key-{version}",
            RecoveryKeyId = $"recovery-key-{version}",
            RecoveryWrappedKey = $"recovery-wrapped-key-{version}",
            KeyCheck = $"key-check-{version}",
        };

    private async Task AddAsync(params PersistedMasterKey[] masterKeys)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);
        foreach (var masterKey in masterKeys)
            await store.AddAsync(masterKey, CancellationToken.None);

        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<PersistedMasterKey>> ListAsync(SecretOwner owner)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).ListAsync(owner, CancellationToken.None);
    }

    private async Task<PersistedMasterKey?> FindActiveAsync(SecretOwner owner)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).FindActiveAsync(owner, CancellationToken.None);
    }

    private async Task<IReadOnlyList<PersistedMasterKey>> FindByKeyIdsAsync(
        SecretOwner owner,
        params Guid[] keyIds
    )
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).FindByKeyIdsAsync(owner, keyIds, CancellationToken.None);
    }

    private async Task<int> GetLatestVersionAsync(SecretOwner owner)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).GetLatestVersionAsync(owner, CancellationToken.None);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddThenList_ReturnsNewestFirst()
    {
        var version1 = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true);
        var version2 = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true);
        var version3 = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 3);
        await AddAsync(version2, version1, version3);

        var masterKeys = await ListAsync(SecretOwner.ForTenant(TenantOneId));

        Assert.Equal(
            [version3.KeyId, version2.KeyId, version1.KeyId],
            masterKeys.Select(masterKey => masterKey.KeyId)
        );
        Assert.Equal([3, 2, 1], masterKeys.Select(masterKey => masterKey.Version));
    }

    [Fact]
    public async Task AddThenList_RoundTripsEveryMember()
    {
        var expected = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1);
        await AddAsync(expected);

        var actual = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));

        Assert.Equal(expected.KeyId, actual.KeyId);
        Assert.Equal(SecretOwnerTypes.Tenant, actual.OwnerType);
        Assert.Equal(TenantOneId, actual.OwnerId);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.CreatedWhen, actual.CreatedWhen);
        Assert.Null(actual.RetiredWhen);
        Assert.Equal(expected.RootKeyProvider, actual.RootKeyProvider);
        Assert.Equal(expected.RootKeyId, actual.RootKeyId);
        Assert.Equal(expected.RootWrappedKey, actual.RootWrappedKey);
        Assert.Equal(expected.RecoveryKeyId, actual.RecoveryKeyId);
        Assert.Equal(expected.RecoveryWrappedKey, actual.RecoveryWrappedKey);
        Assert.Equal(expected.KeyCheck, actual.KeyCheck);
    }

    [Fact]
    public async Task AddAsync_SetsConcurrencyTokenOnItemAndEntity()
    {
        var masterKey = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1);
        await AddAsync(masterKey);

        var actual = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));

        Assert.NotEmpty(masterKey.ConcurrencyToken);
        Assert.Equal(masterKey.ConcurrencyToken, actual.ConcurrencyToken);
    }

    [Fact]
    public async Task Add_DuplicateVersion_SaveChangesThrows()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true));

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context)
            .AddAsync(
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId.ToUpperInvariant(), 1),
                CancellationToken.None
            );

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Add_SecondActiveKeyForOwner_SaveChangesThrows()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context)
            .AddAsync(
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId.ToUpperInvariant(), 2),
                CancellationToken.None
            );

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Add_ActiveKeyAfterRetiringPrevious_Succeeds()
    {
        var owner = SecretOwner.ForTenant(TenantOneId);
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));
        var previous = Assert.Single(await ListAsync(owner));
        previous.RetiredWhen = CreatedWhen.AddDays(30);
        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).UpdateAsync(previous, CancellationToken.None);
            await context.SaveChangesAsync();
        }
        var next = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2);

        await AddAsync(next);

        var active = await FindActiveAsync(owner);
        Assert.NotNull(active);
        Assert.Equal(next.KeyId, active.KeyId);
        Assert.Equal(2, (await ListAsync(owner)).Count);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Update_SetsRetiredWhen()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));
        var retiredWhen = CreatedWhen.AddDays(30);
        var masterKey = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
        masterKey.RetiredWhen = retiredWhen;

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).UpdateAsync(masterKey, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        var actual = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
        Assert.Equal(retiredWhen, actual.RetiredWhen);
    }

    [Fact]
    public async Task Update_CopiesTheMutableMembersAndChangesTheConcurrencyToken()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));
        var masterKey = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
        var originalToken = masterKey.ConcurrencyToken;
        masterKey.RootKeyProvider = KeyWrapperNames.AwsKms;
        masterKey.RootKeyId = "new-root-key";
        masterKey.RootWrappedKey = "new-root-wrapped-key";
        masterKey.RecoveryKeyId = null;
        masterKey.RecoveryWrappedKey = null;

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).UpdateAsync(masterKey, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        var actual = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
        Assert.Equal(KeyWrapperNames.AwsKms, actual.RootKeyProvider);
        Assert.Equal("new-root-key", actual.RootKeyId);
        Assert.Equal("new-root-wrapped-key", actual.RootWrappedKey);
        Assert.Null(actual.RecoveryKeyId);
        Assert.Null(actual.RecoveryWrappedKey);
        Assert.NotEqual(originalToken, actual.ConcurrencyToken);
    }

    [Fact]
    public async Task Update_WhenTheMasterKeyIsMissing_Throws()
    {
        var masterKey = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateAsync(masterKey, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task Update_WhenTheConcurrencyTokenDiffers_Throws()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));
        var masterKey = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
        masterKey.ConcurrencyToken = "stale";

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);

        await Assert.ThrowsAsync<StoreConcurrencyException>(() =>
            store.UpdateAsync(masterKey, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region GetPageAsync Tests

    private async Task<PagedResult<PersistedMasterKey>> GetPageAsync(PageRequest page)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).GetPageAsync(page, CancellationToken.None);
    }

    [Fact]
    public async Task GetPage_ReturnsEveryOwnersRowsAcrossPages()
    {
        var keys = new[]
        {
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 1),
            CreateMasterKey(SecretOwnerTypes.Platform, SecretOwner.PlatformOwnerId, 1),
        };
        await AddAsync(keys);

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var result = await GetPageAsync(new PageRequest { Limit = 3, Cursor = cursor });
            seen.AddRange(result.Items.Select(item => item.KeyId));
            cursor = result.NextCursor;
            pages++;
        } while (cursor is not null);

        Assert.Equal(2, pages);
        Assert.Equal(keys.Select(key => key.KeyId).Order(), seen.Order());
    }

    [Fact]
    public async Task GetPage_MalformedCursor_StartsAtTheBeginning()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));

        var result = await GetPageAsync(new PageRequest { Limit = 10, Cursor = "!!" });

        Assert.Single(result.Items);
        Assert.Null(result.NextCursor);
    }

    #endregion

    #region GetRetiredOwnerPageAsync Tests

    private async Task<List<SecretOwner>> ReadRetiredOwnersAsync(int limit)
    {
        var owners = new List<SecretOwner>();
        string? cursor = null;
        do
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var page = await CreateStore(context)
                .GetRetiredOwnerPageAsync(
                    new PageRequest { Limit = limit, Cursor = cursor },
                    CancellationToken.None
                );
            owners.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        return owners;
    }

    [Fact]
    public async Task GetRetiredOwnerPage_RetiredRowsOfSeveralOwners_ReturnsEachOwnerAcrossPages()
    {
        await AddAsync(
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 3),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 1, retired: true),
            CreateMasterKey(
                SecretOwnerTypes.Platform,
                SecretOwner.PlatformOwnerId,
                1,
                retired: true
            )
        );

        var owners = await ReadRetiredOwnersAsync(limit: 1);

        // An owner with two retired keys appears on two pages, so the caller handles each owner once.
        Assert.Equal(
            [
                SecretOwner.ForTenant(TenantOneId),
                SecretOwner.ForTenant(TenantTwoId),
                SecretOwner.Platform,
            ],
            owners.Distinct()
        );
        Assert.Equal(4, owners.Count);
    }

    [Fact]
    public async Task GetRetiredOwnerPage_OnlyActiveRows_ReturnsNoOwners()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));

        Assert.Empty(await ReadRetiredOwnersAsync(limit: 10));
    }

    [Fact]
    public async Task GetRetiredOwnerPage_TwoRetiredRowsOfOneOwnerOnOnePage_ReturnsTheOwnerOnce()
    {
        await AddAsync(
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true)
        );

        var owner = Assert.Single(await ReadRetiredOwnersAsync(limit: 10));

        Assert.Equal(SecretOwner.ForTenant(TenantOneId), owner);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task Remove_DeletesTheRow()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true));
        var masterKey = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            Assert.True(await CreateStore(context).RemoveAsync(masterKey, CancellationToken.None));
            await context.SaveChangesAsync();
        }

        Assert.Empty(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
    }

    [Fact]
    public async Task Remove_WhenAbsent_ReturnsFalse()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        Assert.False(
            await CreateStore(context)
                .RemoveAsync(
                    CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1),
                    CancellationToken.None
                )
        );
    }

    [Fact]
    public async Task Remove_WhenTheConcurrencyTokenDiffers_Throws()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true));
        var masterKey = Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
        masterKey.ConcurrencyToken = "stale";

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        await Assert.ThrowsAsync<StoreConcurrencyException>(() =>
            CreateStore(context).RemoveAsync(masterKey, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region ListAsync Tests

    [Fact]
    public async Task List_OtherOwner_ReturnsEmpty()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));

        Assert.Empty(await ListAsync(SecretOwner.ForTenant(TenantTwoId)));
        Assert.Empty(await ListAsync(new SecretOwner(SecretOwnerTypes.Platform, TenantOneId)));
    }

    [Fact]
    public async Task List_OwnerIdDifferentCase_Finds()
    {
        var masterKey = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1);
        await AddAsync(masterKey);

        var masterKeys = await ListAsync(
            new SecretOwner(SecretOwnerTypes.Tenant.ToUpperInvariant(), "TENANT-ONE")
        );

        var actual = Assert.Single(masterKeys);
        Assert.Equal(masterKey.KeyId, actual.KeyId);
        Assert.Equal(TenantOneId, actual.OwnerId);
    }

    #endregion

    #region FindActiveAsync Tests

    [Fact]
    public async Task FindActive_ReturnsTheUnretiredKey()
    {
        var retired = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true);
        var active = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2);
        await AddAsync(retired, active);

        var actual = await FindActiveAsync(
            new SecretOwner(SecretOwnerTypes.Tenant.ToUpperInvariant(), "TENANT-ONE")
        );

        Assert.NotNull(actual);
        Assert.Equal(active.KeyId, actual.KeyId);
        Assert.Equal(2, actual.Version);
        Assert.Null(actual.RetiredWhen);
    }

    [Fact]
    public async Task FindActive_NoKeys_ReturnsNull()
    {
        Assert.Null(await FindActiveAsync(SecretOwner.ForTenant(TenantOneId)));
    }

    [Fact]
    public async Task FindActive_OtherOwner_ReturnsNull()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));

        Assert.Null(await FindActiveAsync(SecretOwner.ForTenant(TenantTwoId)));
        Assert.Null(await FindActiveAsync(new SecretOwner(SecretOwnerTypes.Platform, TenantOneId)));
    }

    #endregion

    #region FindByKeyIdsAsync Tests

    [Fact]
    public async Task FindByKeyIds_ReturnsOnlyThatOwnersMatches()
    {
        var retired = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true);
        var active = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2);
        var unrequested = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 3, retired: true);
        var otherOwner = CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 1);
        await AddAsync(retired, active, unrequested, otherOwner);

        var masterKeys = await FindByKeyIdsAsync(
            SecretOwner.ForTenant(TenantOneId.ToUpperInvariant()),
            retired.KeyId,
            active.KeyId,
            otherOwner.KeyId,
            Guid.NewGuid()
        );

        Guid[] expected = [retired.KeyId, active.KeyId];
        Assert.Equal(expected.Order(), masterKeys.Select(masterKey => masterKey.KeyId).Order());
    }

    [Fact]
    public async Task FindByKeyIds_EmptySet_ReturnsEmpty()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1));

        var masterKeys = await FindByKeyIdsAsync(SecretOwner.ForTenant(TenantOneId));

        Assert.Empty(masterKeys);
    }

    #endregion

    #region GetLatestVersionAsync Tests

    [Fact]
    public async Task GetLatestVersion_ReturnsMaxIncludingRetired()
    {
        await AddAsync(
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 5)
        );

        var latestVersion = await GetLatestVersionAsync(
            SecretOwner.ForTenant(TenantOneId.ToUpperInvariant())
        );

        Assert.Equal(2, latestVersion);
    }

    [Fact]
    public async Task GetLatestVersion_NoKeys_ReturnsZero()
    {
        await AddAsync(CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 5));

        var latestVersion = await GetLatestVersionAsync(SecretOwner.ForTenant(TenantOneId));

        Assert.Equal(0, latestVersion);
    }

    #endregion

    #region RemoveAllAsync Tests

    [Fact]
    public async Task RemoveAll_RemovesOnlyThatOwner()
    {
        await AddAsync(
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2),
            CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 1),
            CreateMasterKey(SecretOwnerTypes.Platform, SecretOwner.PlatformOwnerId, 1)
        );

        int removed;
        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            removed = await CreateStore(context)
                .RemoveAllAsync(
                    SecretOwner.ForTenant(TenantOneId.ToUpperInvariant()),
                    CancellationToken.None
                );
            await context.SaveChangesAsync();
        }

        Assert.Equal(2, removed);
        Assert.Empty(await ListAsync(SecretOwner.ForTenant(TenantOneId)));
        Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantTwoId)));
        Assert.Single(await ListAsync(SecretOwner.Platform));
    }

    #endregion
}
