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

    private static DateTimeOffset CreatedWhen { get; } = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private SqliteConnection Connection { get; }
    private ServiceProvider Provider { get; }

    public MasterKeyStoreTests()
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

    private MasterKeyStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), Provider.GetRequiredService<IIdGenerator<long>>(), context);

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
            RootKeyReference = version % 2 == 0 ? $"kms://root-{version}" : null,
            RootKeyAccessIdentity = version % 2 == 0 ? $"role://root-{version}" : null,
            RootWrappedKey = $"root-wrapped-key-{version}",
            RecoveryKeyId = $"recovery-key-{version}",
            RecoveryWrappedKey = $"recovery-wrapped-key-{version}",
            KeyCheck = $"key-check-{version}",
        };

    private async ValueTask AddAsync(
        PersistedMasterKey[] masterKeys,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);
        foreach (var masterKey in masterKeys)
            await store.AddAsync(masterKey, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask<IReadOnlyList<PersistedMasterKey>> ListAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).ListAsync(owner, cancellationToken);
    }

    private async ValueTask<PersistedMasterKey?> FindActiveAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).FindActiveAsync(owner, cancellationToken);
    }

    private async ValueTask<IReadOnlyList<PersistedMasterKey>> FindByKeyIdsAsync(
        SecretOwner owner,
        Guid[] keyIds,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).FindByKeyIdsAsync(owner, keyIds, cancellationToken);
    }

    private async ValueTask<int> GetLatestVersionAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).GetLatestVersionAsync(owner, cancellationToken);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddThenList_ReturnsNewestFirst()
    {
        var version1 = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true);
        var version2 = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true);
        var version3 = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 3);
        await AddAsync([version2, version1, version3], CancellationToken.None);

        var masterKeys = await ListAsync(
            SecretOwner.ForTenant(TenantOneId),
            CancellationToken.None
        );

        Assert.Equal(
            [version3.KeyId, version2.KeyId, version1.KeyId],
            masterKeys.Select(masterKey => masterKey.KeyId)
        );
        Assert.Equal([3, 2, 1], masterKeys.Select(masterKey => masterKey.Version));
    }

    [Fact]
    public async Task AddThenList_RoundTripsEveryMember()
    {
        var expected = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2);
        await AddAsync([expected], CancellationToken.None);

        var actual = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
        Assert.NotNull(actual.RootKeyReference);
        Assert.NotNull(actual.RootKeyAccessIdentity);

        Assert.Equal(expected.KeyId, actual.KeyId);
        Assert.Equal(SecretOwnerTypes.Tenant, actual.OwnerType);
        Assert.Equal(TenantOneId, actual.OwnerId);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.CreatedWhen, actual.CreatedWhen);
        Assert.Null(actual.RetiredWhen);
        Assert.Equal(expected.RootKeyProvider, actual.RootKeyProvider);
        Assert.Equal(expected.RootKeyId, actual.RootKeyId);
        Assert.Equal(expected.RootKeyReference, actual.RootKeyReference);
        Assert.Equal(expected.RootKeyAccessIdentity, actual.RootKeyAccessIdentity);
        Assert.Equal(expected.RootWrappedKey, actual.RootWrappedKey);
        Assert.Equal(expected.RecoveryKeyId, actual.RecoveryKeyId);
        Assert.Equal(expected.RecoveryWrappedKey, actual.RecoveryWrappedKey);
        Assert.Equal(expected.KeyCheck, actual.KeyCheck);
    }

    [Fact]
    public async Task AddAsync_SetsConcurrencyTokenOnItemAndEntity()
    {
        var masterKey = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1);
        await AddAsync([masterKey], CancellationToken.None);

        var actual = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );

        Assert.NotEmpty(masterKey.ConcurrencyToken);
        Assert.Equal(masterKey.ConcurrencyToken, actual.ConcurrencyToken);
    }

    [Fact]
    public async Task Add_DuplicateVersion_SaveChangesThrows()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true)],
            CancellationToken.None
        );

        using var scope = Provider.CreateScope();
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
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );

        using var scope = Provider.CreateScope();
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
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );
        var previous = Assert.Single(await ListAsync(owner, CancellationToken.None));
        previous.RetiredWhen = CreatedWhen.AddDays(30);
        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).UpdateAsync(previous, CancellationToken.None);
            await context.SaveChangesAsync();
        }
        var next = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2);

        await AddAsync([next], CancellationToken.None);

        var active = await FindActiveAsync(owner, CancellationToken.None);
        Assert.NotNull(active);
        Assert.Equal(next.KeyId, active.KeyId);
        Assert.Equal(2, (await ListAsync(owner, CancellationToken.None)).Count);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Update_SetsRetiredWhen()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );
        var retiredWhen = CreatedWhen.AddDays(30);
        var masterKey = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
        masterKey.RetiredWhen = retiredWhen;

        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).UpdateAsync(masterKey, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        var actual = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
        Assert.Equal(retiredWhen, actual.RetiredWhen);
    }

    [Fact]
    public async Task Update_CopiesTheMutableMembersAndChangesTheConcurrencyToken()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );
        var masterKey = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
        var originalToken = masterKey.ConcurrencyToken;
        masterKey.RootKeyProvider = KeyWrapperNames.AwsKms;
        masterKey.RootKeyId = "new-root-key";
        masterKey.RootWrappedKey = "new-root-wrapped-key";
        masterKey.RecoveryKeyId = null;
        masterKey.RecoveryWrappedKey = null;

        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).UpdateAsync(masterKey, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        var actual = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
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

        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateAsync(masterKey, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task Update_WhenTheConcurrencyTokenDiffers_Throws()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );
        var masterKey = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
        masterKey.ConcurrencyToken = "stale";

        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);

        await Assert.ThrowsAsync<StoreConcurrencyException>(() =>
            store.UpdateAsync(masterKey, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region GetPageAsync Tests

    private async ValueTask<PagedResult<PersistedMasterKey>> GetPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        return await CreateStore(context).GetPageAsync(page, cancellationToken);
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
        await AddAsync(keys, CancellationToken.None);

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var result = await GetPageAsync(
                new PageRequest { Limit = 3, Cursor = cursor },
                CancellationToken.None
            );
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
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );

        var result = await GetPageAsync(
            new PageRequest { Limit = 10, Cursor = "!!" },
            CancellationToken.None
        );

        Assert.Single(result.Items);
        Assert.Null(result.NextCursor);
    }

    #endregion

    #region GetRetiredOwnerPageAsync Tests

    private async ValueTask<List<SecretOwner>> ReadRetiredOwnersAsync(
        int limit,
        CancellationToken cancellationToken
    )
    {
        var owners = new List<SecretOwner>();
        string? cursor = null;
        do
        {
            using var scope = Provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var page = await CreateStore(context)
                .GetRetiredOwnerPageAsync(
                    new PageRequest { Limit = limit, Cursor = cursor },
                    cancellationToken
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
            [
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 3),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 1, retired: true),
                CreateMasterKey(
                    SecretOwnerTypes.Platform,
                    SecretOwner.PlatformOwnerId,
                    1,
                    retired: true
                ),
            ],
            CancellationToken.None
        );

        var owners = await ReadRetiredOwnersAsync(limit: 1, CancellationToken.None);

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
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );

        Assert.Empty(await ReadRetiredOwnersAsync(limit: 10, CancellationToken.None));
    }

    [Fact]
    public async Task GetRetiredOwnerPage_TwoRetiredRowsOfOneOwnerOnOnePage_ReturnsTheOwnerOnce()
    {
        await AddAsync(
            [
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true),
            ],
            CancellationToken.None
        );

        var owner = Assert.Single(await ReadRetiredOwnersAsync(limit: 10, CancellationToken.None));

        Assert.Equal(SecretOwner.ForTenant(TenantOneId), owner);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task Remove_DeletesTheRow()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true)],
            CancellationToken.None
        );
        var masterKey = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );

        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            Assert.True(await CreateStore(context).RemoveAsync(masterKey, CancellationToken.None));
            await context.SaveChangesAsync();
        }

        Assert.Empty(await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None));
    }

    [Fact]
    public async Task Remove_WhenAbsent_ReturnsFalse()
    {
        using var scope = Provider.CreateScope();
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
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true)],
            CancellationToken.None
        );
        var masterKey = Assert.Single(
            await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
        masterKey.ConcurrencyToken = "stale";

        using var scope = Provider.CreateScope();
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
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );

        Assert.Empty(await ListAsync(SecretOwner.ForTenant(TenantTwoId), CancellationToken.None));
        Assert.Empty(
            await ListAsync(
                new SecretOwner(SecretOwnerTypes.Platform, TenantOneId),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task List_OwnerIdDifferentCase_Finds()
    {
        var masterKey = CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1);
        await AddAsync([masterKey], CancellationToken.None);

        var masterKeys = await ListAsync(
            new SecretOwner(SecretOwnerTypes.Tenant.ToUpperInvariant(), "TENANT-ONE"),
            CancellationToken.None
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
        await AddAsync([retired, active], CancellationToken.None);

        var actual = await FindActiveAsync(
            new SecretOwner(SecretOwnerTypes.Tenant.ToUpperInvariant(), "TENANT-ONE"),
            CancellationToken.None
        );

        Assert.NotNull(actual);
        Assert.Equal(active.KeyId, actual.KeyId);
        Assert.Equal(2, actual.Version);
        Assert.Null(actual.RetiredWhen);
    }

    [Fact]
    public async Task FindActive_NoKeys_ReturnsNull()
    {
        Assert.Null(
            await FindActiveAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None)
        );
    }

    [Fact]
    public async Task FindActive_OtherOwner_ReturnsNull()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );

        Assert.Null(
            await FindActiveAsync(SecretOwner.ForTenant(TenantTwoId), CancellationToken.None)
        );
        Assert.Null(
            await FindActiveAsync(
                new SecretOwner(SecretOwnerTypes.Platform, TenantOneId),
                CancellationToken.None
            )
        );
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
        await AddAsync([retired, active, unrequested, otherOwner], CancellationToken.None);

        var masterKeys = await FindByKeyIdsAsync(
            SecretOwner.ForTenant(TenantOneId.ToUpperInvariant()),
            [retired.KeyId, active.KeyId, otherOwner.KeyId, Guid.NewGuid()],
            CancellationToken.None
        );

        Guid[] expected = [retired.KeyId, active.KeyId];
        Assert.Equal(expected.Order(), masterKeys.Select(masterKey => masterKey.KeyId).Order());
    }

    [Fact]
    public async Task FindByKeyIds_EmptySet_ReturnsEmpty()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1)],
            CancellationToken.None
        );

        var masterKeys = await FindByKeyIdsAsync(
            SecretOwner.ForTenant(TenantOneId),
            [],
            CancellationToken.None
        );

        Assert.Empty(masterKeys);
    }

    #endregion

    #region GetLatestVersionAsync Tests

    [Fact]
    public async Task GetLatestVersion_ReturnsMaxIncludingRetired()
    {
        await AddAsync(
            [
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2, retired: true),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 5),
            ],
            CancellationToken.None
        );

        var latestVersion = await GetLatestVersionAsync(
            SecretOwner.ForTenant(TenantOneId.ToUpperInvariant()),
            CancellationToken.None
        );

        Assert.Equal(2, latestVersion);
    }

    [Fact]
    public async Task GetLatestVersion_NoKeys_ReturnsZero()
    {
        await AddAsync(
            [CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 5)],
            CancellationToken.None
        );

        var latestVersion = await GetLatestVersionAsync(
            SecretOwner.ForTenant(TenantOneId),
            CancellationToken.None
        );

        Assert.Equal(0, latestVersion);
    }

    #endregion

    #region RemoveAllAsync Tests

    [Fact]
    public async Task RemoveAll_RemovesOnlyThatOwner()
    {
        await AddAsync(
            [
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 1, retired: true),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantOneId, 2),
                CreateMasterKey(SecretOwnerTypes.Tenant, TenantTwoId, 1),
                CreateMasterKey(SecretOwnerTypes.Platform, SecretOwner.PlatformOwnerId, 1),
            ],
            CancellationToken.None
        );

        int removed;
        using (var scope = Provider.CreateScope())
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
        Assert.Empty(await ListAsync(SecretOwner.ForTenant(TenantOneId), CancellationToken.None));
        Assert.Single(await ListAsync(SecretOwner.ForTenant(TenantTwoId), CancellationToken.None));
        Assert.Single(await ListAsync(SecretOwner.Platform, CancellationToken.None));
    }

    #endregion
}
