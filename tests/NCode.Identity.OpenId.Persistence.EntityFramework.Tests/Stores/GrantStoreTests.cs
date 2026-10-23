#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class GrantStoreTests : IDisposable
{
    private const string GrantType = "authorization_code";
    private const string HashedKey = "hashed-key-1";
    private const string TenantId = "tenant-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly GrantStore _store;
    private readonly TenantStore _tenantStore;

    public GrantStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);

        SeedTenantAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new Dictionary<string, object>());

    private async Task SeedTenantAsync()
    {
        await _tenantStore.AddAsync(
            new PersistedTenant
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Issuer = null,
                IsDisabled = false,
                DisplayName = "Tenant One",
                Settings = new PersistedTenantSettings
                {
                    TenantId = TenantId,
                    ConcurrencyToken = string.Empty,
                    Value = EmptyObject(),
                },
                Secrets = new PersistedTenantSecrets
                {
                    TenantId = TenantId,
                    ConcurrencyToken = string.Empty,
                    Value = [],
                },
            },
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private static PersistedGrant CreateGrant(
        string hashedKey = HashedKey,
        string grantId = "grant-1",
        string? subjectId = "subject-1",
        string? sessionId = null,
        string? refreshTokenLineageId = null
    ) =>
        new()
        {
            GrantType = GrantType,
            HashedKey = hashedKey,
            GrantId = grantId,
            ConcurrencyToken = string.Empty,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = subjectId,
            SessionId = sessionId,
            RefreshTokenLineageId = refreshTokenLineageId,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = null,
            ConsumedWhen = null,
            PayloadJson = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
        };

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_AssignsConcurrencyToken()
    {
        await _store.AddAsync(CreateGrant(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.False(string.IsNullOrEmpty(reloaded.ConcurrencyToken));
    }

    [Fact]
    public async Task AddAsync_PersistsGrantId()
    {
        await _store.AddAsync(CreateGrant(grantId: "grant-abc"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync("grant-abc", CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("grant-abc", reloaded.GrantId);
    }

    #endregion

    #region GetOrDefaultAsync Tests

    [Fact]
    public async Task GetOrDefaultAsync_ByGrantId_WhenMissing_ReturnsNull()
    {
        var result = await _store.GetOrDefaultAsync("no-such-grant", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region GetPageAsync Tests

    [Fact]
    public async Task GetPageAsync_KeysetPaginatesInOrder()
    {
        for (var index = 0; index < 3; index++)
        {
            await _store.AddAsync(
                CreateGrant(hashedKey: $"hashed-key-{index}", grantId: $"grant-{index}"),
                CancellationToken.None
            );
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var firstPage = await _store.GetPageAsync(
            new GrantFilter(),
            new PageRequest { Limit = 2 },
            CancellationToken.None
        );

        Assert.Equal(2, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await _store.GetPageAsync(
            new GrantFilter(),
            new PageRequest { Cursor = firstPage.NextCursor, Limit = 2 },
            CancellationToken.None
        );

        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);

        var seen = firstPage.Items.Concat(secondPage.Items).Select(grant => grant.GrantId).ToList();
        Assert.Equal(new[] { "grant-0", "grant-1", "grant-2" }, seen);
    }

    [Fact]
    public async Task GetPageAsync_FiltersBySubject()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a", grantId: "grant-a", subjectId: "alice"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-b", grantId: "grant-b", subjectId: "bob"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var page = await _store.GetPageAsync(
            new GrantFilter { SubjectId = "alice" },
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Single(page.Items);
        Assert.Equal("grant-a", page.Items[0].GrantId);
    }

    [Fact]
    public async Task GetPageAsync_FiltersBySession()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-s1", grantId: "grant-s1", sessionId: "sid-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-s2", grantId: "grant-s2", sessionId: "sid-2"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var page = await _store.GetPageAsync(
            new GrantFilter { SessionId = "sid-1" },
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Single(page.Items);
        Assert.Equal("grant-s1", page.Items[0].GrantId);
    }

    #endregion

    #region RevokeWhereAsync Tests

    [Fact]
    public async Task RevokeWhereAsync_BySubject_RevokesOnlyActiveMatches()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a1", grantId: "grant-a1", subjectId: "alice"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a2", grantId: "grant-a2", subjectId: "alice"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-b", grantId: "grant-b", subjectId: "bob"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await _store.RevokeWhereAsync(
            new GrantFilter { SubjectId = "alice" },
            revokedWhen,
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(2, count);

        var alice1 = await _store.GetOrDefaultAsync("grant-a1", CancellationToken.None);
        var bob = await _store.GetOrDefaultAsync("grant-b", CancellationToken.None);
        Assert.NotNull(alice1);
        Assert.NotNull(bob);
        Assert.Equal(revokedWhen, alice1.RevokedWhen);
        Assert.Null(bob.RevokedWhen);
    }

    [Fact]
    public async Task RevokeWhereAsync_IsIdempotentForAlreadyRevoked()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a", grantId: "grant-a", subjectId: "alice"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var first = await _store.RevokeWhereAsync(
            new GrantFilter { SubjectId = "alice" },
            DateTimeOffset.UnixEpoch.AddMinutes(10),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var second = await _store.RevokeWhereAsync(
            new GrantFilter { SubjectId = "alice" },
            DateTimeOffset.UnixEpoch.AddMinutes(20),
            CancellationToken.None
        );

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task RevokeWhereAsync_BySession_RevokesOnlyThatSessionsGrants()
    {
        await _store.AddAsync(
            CreateGrant(
                hashedKey: "hk-s1",
                grantId: "grant-s1",
                subjectId: "alice",
                sessionId: "sid-1"
            ),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(
                hashedKey: "hk-s2",
                grantId: "grant-s2",
                subjectId: "alice",
                sessionId: "sid-2"
            ),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await _store.RevokeWhereAsync(
            new GrantFilter { SessionId = "sid-1" },
            revokedWhen,
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(1, count);

        var session1 = await _store.GetOrDefaultAsync("grant-s1", CancellationToken.None);
        var session2 = await _store.GetOrDefaultAsync("grant-s2", CancellationToken.None);
        Assert.NotNull(session1);
        Assert.NotNull(session2);
        Assert.Equal(revokedWhen, session1.RevokedWhen);
        Assert.Null(session2.RevokedWhen);
    }

    [Fact]
    public async Task RevokeWhereAsync_WhenFilteredByGrantId_RevokesOnlyThatGrant()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-g1", grantId: "grant-1", subjectId: "alice"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-g2", grantId: "grant-2", subjectId: "alice"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await _store.RevokeWhereAsync(
            new GrantFilter { GrantId = "grant-2" },
            revokedWhen,
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(1, count);

        var grant1 = await _store.GetOrDefaultAsync("grant-1", CancellationToken.None);
        var grant2 = await _store.GetOrDefaultAsync("grant-2", CancellationToken.None);
        Assert.NotNull(grant1);
        Assert.NotNull(grant2);
        Assert.Null(grant1.RevokedWhen);
        Assert.Equal(revokedWhen, grant2.RevokedWhen);
    }

    #endregion

    #region RevokeByRefreshTokenLineageAsync Tests

    [Fact]
    public async Task RevokeByRefreshTokenLineageAsync_RevokesOnlyActiveFamilyMembers()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-f1", grantId: "grant-f1", refreshTokenLineageId: "fam-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-f2", grantId: "grant-f2", refreshTokenLineageId: "fam-1"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(
                hashedKey: "hk-other",
                grantId: "grant-other",
                refreshTokenLineageId: "fam-2"
            ),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await _store.RevokeByRefreshTokenLineageAsync(
            "fam-1",
            revokedWhen,
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(2, count);

        var family1 = await _store.GetOrDefaultAsync("grant-f1", CancellationToken.None);
        var family2 = await _store.GetOrDefaultAsync("grant-f2", CancellationToken.None);
        var other = await _store.GetOrDefaultAsync("grant-other", CancellationToken.None);
        Assert.NotNull(family1);
        Assert.NotNull(family2);
        Assert.NotNull(other);
        Assert.Equal(revokedWhen, family1.RevokedWhen);
        Assert.Equal(revokedWhen, family2.RevokedWhen);
        Assert.Null(other.RevokedWhen);
    }

    [Fact]
    public async Task RevokeByRefreshTokenLineageAsync_IsIdempotentForAlreadyRevoked()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-f", grantId: "grant-f", refreshTokenLineageId: "fam-1"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var first = await _store.RevokeByRefreshTokenLineageAsync(
            "fam-1",
            DateTimeOffset.UnixEpoch.AddMinutes(10),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var second = await _store.RevokeByRefreshTokenLineageAsync(
            "fam-1",
            DateTimeOffset.UnixEpoch.AddMinutes(20),
            CancellationToken.None
        );

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    #endregion

    #region GetActiveByRefreshTokenLineageAsync Tests

    [Fact]
    public async Task GetActiveByRefreshTokenLineageAsync_ReturnsTheUnrevokedGrantsOfTheLineage()
    {
        var revoked = CreateGrant(
            hashedKey: "hk-revoked",
            grantId: "grant-revoked",
            refreshTokenLineageId: "lineage-1"
        );
        revoked.RevokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);

        await _store.AddAsync(
            CreateGrant(
                hashedKey: "hk-active",
                grantId: "grant-active",
                refreshTokenLineageId: "lineage-1"
            ),
            CancellationToken.None
        );
        await _store.AddAsync(revoked, CancellationToken.None);
        await _store.AddAsync(
            CreateGrant(
                hashedKey: "hk-other",
                grantId: "grant-other",
                refreshTokenLineageId: "lineage-2"
            ),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var grants = await _store.GetActiveByRefreshTokenLineageAsync(
            "lineage-1",
            CancellationToken.None
        );

        var grant = Assert.Single(grants);
        Assert.Equal("grant-active", grant.GrantId);
        Assert.Equal(TenantId, grant.TenantId);
        Assert.Equal("lineage-1", grant.RefreshTokenLineageId);
        Assert.Null(grant.RevokedWhen);
    }

    [Fact]
    public async Task GetActiveByRefreshTokenLineageAsync_ReturnsGrantsTheUnitOfWorkCanUpdate()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-f", grantId: "grant-f", refreshTokenLineageId: "lineage-1"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var grants = await _store.GetActiveByRefreshTokenLineageAsync(
            "lineage-1",
            CancellationToken.None
        );
        var grant = Assert.Single(grants);
        var concurrencyToken = grant.ConcurrencyToken;

        await _store.UpdateAsync(grant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _store.GetOrDefaultAsync("grant-f", CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.NotEqual(concurrencyToken, reloaded.ConcurrencyToken);
    }

    #endregion

    #region RemoveEndedBeforeAsync Tests

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantExpiredBeforeTheCutoff_RemovesIt()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var expired = CreateGrant(hashedKey: "hk-expired", grantId: "grant-expired");
        expired.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await _store.AddAsync(expired, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveEndedBeforeAsync(cutoff, 10, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(1, removed);
        Assert.Null(await _store.GetOrDefaultAsync("grant-expired", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantWasRevokedBeforeTheCutoff_RemovesItBeforeItExpires()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var revoked = CreateGrant(hashedKey: "hk-revoked", grantId: "grant-revoked");
        revoked.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(30);
        revoked.RevokedWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await _store.AddAsync(revoked, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveEndedBeforeAsync(cutoff, 10, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(1, removed);
        Assert.Null(await _store.GetOrDefaultAsync("grant-revoked", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantHasNotEnded_KeepsIt()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var expiresLater = CreateGrant(hashedKey: "hk-later", grantId: "grant-later");
        expiresLater.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(20);
        var neverEnds = CreateGrant(hashedKey: "hk-never", grantId: "grant-never");
        neverEnds.ExpiresWhen = null;

        await _store.AddAsync(expiresLater, CancellationToken.None);
        await _store.AddAsync(neverEnds, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveEndedBeforeAsync(cutoff, 10, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(0, removed);
        Assert.NotNull(await _store.GetOrDefaultAsync("grant-later", CancellationToken.None));
        Assert.NotNull(await _store.GetOrDefaultAsync("grant-never", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_RemovesAtMostTheLimit()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        for (var index = 0; index < 3; ++index)
        {
            var ended = CreateGrant(hashedKey: $"hk-{index}", grantId: $"grant-{index}");
            ended.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
            await _store.AddAsync(ended, CancellationToken.None);
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveEndedBeforeAsync(cutoff, 2, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(2, removed);
        Assert.Equal(1, await _dbContext.Grants.CountAsync());
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenTheLimitIsLessThanOne_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _store.RemoveEndedBeforeAsync(
                DateTimeOffset.UnixEpoch.AddDays(10),
                0,
                CancellationToken.None
            )
        );
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ChangesConcurrencyToken()
    {
        await _store.AddAsync(CreateGrant(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var current = await _store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);
        Assert.NotNull(current);

        current.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(5);
        await _store.UpdateAsync(current, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(5), reloaded.ConsumedWhen);
    }

    [Fact]
    public async Task UpdateAsync_WhenTokenMismatch_Throws()
    {
        await _store.AddAsync(CreateGrant(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var stale = CreateGrant();
        stale.ConcurrencyToken = "stale-token";
        stale.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(5);

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateAsync(stale, CancellationToken.None)
        );
    }

    #endregion
}
