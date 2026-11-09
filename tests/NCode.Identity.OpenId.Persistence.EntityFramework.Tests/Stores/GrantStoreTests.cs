// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class GrantStoreTests : IAsyncLifetime, IDisposable
{
    private const string GrantType = "authorization_code";
    private const string SessionGrantType = "session";
    private const string HashedKey = "hashed-key-1";
    private const string TenantId = "tenant-1";

    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private GrantStore Store { get; }
    private TenantStore TenantStore { get; }

    public GrantStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new GrantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        TenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
    }

    public Task InitializeAsync() => SeedTenantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private async Task SeedTenantAsync()
    {
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
        var tenant = new PersistedTenantBuilder
        {
            TenantId = TenantId,
            DisplayName = "Tenant One",
        }.Build();
        await TenantStore.AddAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task AddTenantAsync(string tenantId, string teamId)
    {
        var tenant = new PersistedTenantBuilder { TenantId = tenantId, TeamId = teamId }.Build();
        await TenantStore.AddAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task AddExpiredSessionGrantAsync(string grantId, string tenantId)
    {
        var grant = CreateGrant(
            hashedKey: $"hk-{grantId}",
            grantId: grantId,
            grantType: SessionGrantType,
            tenantId: tenantId
        );
        grant.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        await Store.AddAsync(grant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private static PersistedGrant CreateGrant(
        string hashedKey = HashedKey,
        string grantId = "grant-1",
        string? subjectId = "subject-1",
        string? sessionId = null,
        string? refreshTokenLineageId = null,
        string grantType = GrantType,
        string tenantId = TenantId,
        string payload = "protected-payload"
    ) =>
        new()
        {
            GrantType = grantType,
            HashedKey = hashedKey,
            GrantId = grantId,
            ConcurrencyToken = string.Empty,
            TenantId = tenantId,
            ClientId = null,
            SubjectId = subjectId,
            SessionId = sessionId,
            RefreshTokenLineageId = refreshTokenLineageId,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = null,
            ConsumedWhen = null,
            ProtectedPayload = payload,
        };

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_AssignsConcurrencyToken()
    {
        await Store.AddAsync(CreateGrant(), CancellationToken.None);
        await DbContext.SaveChangesAsync();

        var reloaded = await Store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.False(string.IsNullOrEmpty(reloaded.ConcurrencyToken));
    }

    [Fact]
    public async Task AddAsync_PersistsGrantId()
    {
        await Store.AddAsync(CreateGrant(grantId: "grant-abc"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await Store.GetOrDefaultAsync("grant-abc", CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("grant-abc", reloaded.GrantId);
    }

    #endregion

    #region GetOrDefaultAsync Tests

    [Fact]
    public async Task GetOrDefaultAsync_ByGrantId_WhenMissing_ReturnsNull()
    {
        var result = await Store.GetOrDefaultAsync("no-such-grant", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region GetPageAsync Tests

    [Fact]
    public async Task GetPageAsync_KeysetPaginatesInOrder()
    {
        for (var index = 0; index < 3; index++)
        {
            await Store.AddAsync(
                CreateGrant(hashedKey: $"hashed-key-{index}", grantId: $"grant-{index}"),
                CancellationToken.None
            );
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var firstPage = await Store.GetPageAsync(
            new GrantFilter(),
            new PageRequest { Limit = 2 },
            CancellationToken.None
        );

        Assert.Equal(2, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await Store.GetPageAsync(
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
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-a", grantId: "grant-a", subjectId: "alice"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-b", grantId: "grant-b", subjectId: "bob"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetPageAsync(
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
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-s1", grantId: "grant-s1", sessionId: "sid-1"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-s2", grantId: "grant-s2", sessionId: "sid-2"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetPageAsync(
            new GrantFilter { SessionId = "sid-1" },
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Single(page.Items);
        Assert.Equal("grant-s1", page.Items[0].GrantId);
    }

    #endregion

    #region GetUnrevokedExpiredPageAsync Tests

    [Fact]
    public async Task GetUnrevokedExpiredPageAsync_ReturnsTheUnrevokedGrantsOfTheTypeThatExpired()
    {
        var expiredAt = DateTimeOffset.UnixEpoch.AddDays(10);
        var expired = CreateGrant(
            hashedKey: "hk-expired",
            grantId: "grant-expired",
            grantType: SessionGrantType
        );
        expired.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        var withoutExpiry = CreateGrant(
            hashedKey: "hk-without-expiry",
            grantId: "grant-without-expiry",
            grantType: SessionGrantType
        );
        withoutExpiry.ExpiresWhen = null;
        var revoked = CreateGrant(
            hashedKey: "hk-revoked",
            grantId: "grant-revoked",
            grantType: SessionGrantType
        );
        revoked.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        revoked.RevokedWhen = DateTimeOffset.UnixEpoch.AddDays(2);
        var notExpired = CreateGrant(
            hashedKey: "hk-later",
            grantId: "grant-later",
            grantType: SessionGrantType
        );
        notExpired.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(20);
        var otherType = CreateGrant(hashedKey: "hk-other", grantId: "grant-other");
        otherType.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await Store.AddAsync(expired, CancellationToken.None);
        await Store.AddAsync(withoutExpiry, CancellationToken.None);
        await Store.AddAsync(revoked, CancellationToken.None);
        await Store.AddAsync(notExpired, CancellationToken.None);
        await Store.AddAsync(otherType, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            expiredAt,
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Equal("grant-expired", Assert.Single(page.Items).GrantId);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetUnrevokedExpiredPageAsync_SkipsTheGrantsOfADisabledTenant()
    {
        const string disabledTenantId = "tenant-disabled";
        var disabledTenant = new PersistedTenantBuilder
        {
            TenantId = disabledTenantId,
            IsDisabled = true,
            DisplayName = "Tenant Disabled",
        }.Build();
        await TenantStore.AddAsync(disabledTenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var enabledGrant = CreateGrant(
            hashedKey: "hk-enabled",
            grantId: "grant-enabled",
            grantType: SessionGrantType
        );
        enabledGrant.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        var disabledGrant = CreateGrant(
            hashedKey: "hk-disabled",
            grantId: "grant-disabled",
            grantType: SessionGrantType,
            tenantId: disabledTenantId
        );
        disabledGrant.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await Store.AddAsync(enabledGrant, CancellationToken.None);
        await Store.AddAsync(disabledGrant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            DateTimeOffset.UnixEpoch.AddDays(10),
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Single(page.Items);
        Assert.Equal("grant-enabled", page.Items[0].GrantId);
    }

    [Fact]
    public async Task GetUnrevokedExpiredPageAsync_SkipsTheGrantsOfATenantWhoseTeamIsDisabled()
    {
        const string disabledTeamId = "disabled-team";
        const string tenantId = "tenant-of-disabled-team";
        var team = DbContext.SeedTeam(
            Provider.GetRequiredService<IIdGenerator<long>>(),
            disabledTeamId
        );
        team.IsDisabled = true;
        await DbContext.SaveChangesAsync();

        var tenant = new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = disabledTeamId,
            DisplayName = "Tenant Of Disabled Team",
        }.Build();
        await TenantStore.AddAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var enabledGrant = CreateGrant(
            hashedKey: "hk-enabled",
            grantId: "grant-enabled",
            grantType: SessionGrantType
        );
        enabledGrant.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        var skippedGrant = CreateGrant(
            hashedKey: "hk-skipped",
            grantId: "grant-skipped",
            grantType: SessionGrantType,
            tenantId: tenantId
        );
        skippedGrant.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await Store.AddAsync(enabledGrant, CancellationToken.None);
        await Store.AddAsync(skippedGrant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            DateTimeOffset.UnixEpoch.AddDays(10),
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Equal("grant-enabled", Assert.Single(page.Items).GrantId);
    }

    [Fact]
    public async Task GetUnrevokedExpiredPageAsync_SkipsTheGrantsOfATenantBeneathADisabledTeam()
    {
        const string childTeamId = "disabled-parent-web";
        const string tenantId = "tenant-beneath-disabled-team";
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        var parent = DbContext.SeedTeam(idGenerator, "disabled-parent");
        parent.IsDisabled = true;
        await DbContext.SaveChangesAsync();
        DbContext.SeedChildTeam(idGenerator, parent, childTeamId);

        var tenant = new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = childTeamId,
            DisplayName = "Tenant Beneath Disabled Team",
        }.Build();
        await TenantStore.AddAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var enabledGrant = CreateGrant(
            hashedKey: "hk-enabled",
            grantId: "grant-enabled",
            grantType: SessionGrantType
        );
        enabledGrant.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        var skippedGrant = CreateGrant(
            hashedKey: "hk-skipped",
            grantId: "grant-skipped",
            grantType: SessionGrantType,
            tenantId: tenantId
        );
        skippedGrant.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await Store.AddAsync(enabledGrant, CancellationToken.None);
        await Store.AddAsync(skippedGrant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            DateTimeOffset.UnixEpoch.AddDays(10),
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Equal("grant-enabled", Assert.Single(page.Items).GrantId);
    }

    [Fact]
    public async Task GetUnrevokedExpiredPageAsync_SkipsTheGrantsOfATenantBeneathADisabledGrandparentTeam()
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        var grandparent = DbContext.SeedTeam(idGenerator, "acme");
        grandparent.IsDisabled = true;
        await DbContext.SaveChangesAsync();
        var parent = DbContext.SeedChildTeam(idGenerator, grandparent, "acme-web");
        DbContext.SeedChildTeam(idGenerator, parent, "acme-web-eu");
        DbContext.SeedTeam(idGenerator, "other");
        DbContext.ChangeTracker.Clear();

        await AddTenantAsync("tenant-beneath-grandparent", "acme-web-eu");
        await AddTenantAsync("tenant-of-other-team", "other");
        await AddExpiredSessionGrantAsync("grant-skipped", "tenant-beneath-grandparent");
        await AddExpiredSessionGrantAsync("grant-enabled", "tenant-of-other-team");

        var page = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            DateTimeOffset.UnixEpoch.AddDays(10),
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Equal("grant-enabled", Assert.Single(page.Items).GrantId);
    }

    [Fact]
    public async Task GetUnrevokedExpiredPageAsync_ReturnsTheGrantsOfATeamWhoseIdentifierExtendsADisabledTeams()
    {
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        var disabled = DbContext.SeedTeam(idGenerator, "acme");
        disabled.IsDisabled = true;
        await DbContext.SaveChangesAsync();
        DbContext.SeedTeam(idGenerator, "acme-x");
        DbContext.ChangeTracker.Clear();

        await AddTenantAsync("tenant-of-acme-x", "acme-x");
        await AddExpiredSessionGrantAsync("grant-returned", "tenant-of-acme-x");

        var page = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            DateTimeOffset.UnixEpoch.AddDays(10),
            new PageRequest { Limit = 50 },
            CancellationToken.None
        );

        Assert.Equal("grant-returned", Assert.Single(page.Items).GrantId);
    }

    [Fact]
    public async Task GetUnrevokedExpiredPageAsync_KeysetPaginatesInOrder()
    {
        for (var index = 0; index < 3; index++)
        {
            var expired = CreateGrant(
                hashedKey: $"hashed-key-{index}",
                grantId: $"grant-{index}",
                grantType: SessionGrantType
            );
            expired.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
            await Store.AddAsync(expired, CancellationToken.None);
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var expiredAt = DateTimeOffset.UnixEpoch.AddDays(10);

        var firstPage = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            expiredAt,
            new PageRequest { Limit = 2 },
            CancellationToken.None
        );

        Assert.Equal(2, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await Store.GetUnrevokedExpiredPageAsync(
            SessionGrantType,
            expiredAt,
            new PageRequest { Cursor = firstPage.NextCursor, Limit = 2 },
            CancellationToken.None
        );

        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);

        var seen = firstPage.Items.Concat(secondPage.Items).Select(grant => grant.GrantId).ToList();
        Assert.Equal(new[] { "grant-0", "grant-1", "grant-2" }, seen);
    }

    #endregion

    #region RevokeWhereAsync Tests

    [Fact]
    public async Task RevokeWhereAsync_BySubject_RevokesOnlyActiveMatches()
    {
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-a1", grantId: "grant-a1", subjectId: "alice"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-a2", grantId: "grant-a2", subjectId: "alice"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-b", grantId: "grant-b", subjectId: "bob"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await Store.RevokeWhereAsync(
            new GrantFilter { SubjectId = "alice" },
            revokedWhen,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(2, count);

        var alice1 = await Store.GetOrDefaultAsync("grant-a1", CancellationToken.None);
        var bob = await Store.GetOrDefaultAsync("grant-b", CancellationToken.None);
        Assert.NotNull(alice1);
        Assert.NotNull(bob);
        Assert.Equal(revokedWhen, alice1.RevokedWhen);
        Assert.Null(bob.RevokedWhen);
    }

    [Fact]
    public async Task RevokeWhereAsync_IsIdempotentForAlreadyRevoked()
    {
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-a", grantId: "grant-a", subjectId: "alice"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var first = await Store.RevokeWhereAsync(
            new GrantFilter { SubjectId = "alice" },
            DateTimeOffset.UnixEpoch.AddMinutes(10),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var second = await Store.RevokeWhereAsync(
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
        await Store.AddAsync(
            CreateGrant(
                hashedKey: "hk-s1",
                grantId: "grant-s1",
                subjectId: "alice",
                sessionId: "sid-1"
            ),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(
                hashedKey: "hk-s2",
                grantId: "grant-s2",
                subjectId: "alice",
                sessionId: "sid-2"
            ),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await Store.RevokeWhereAsync(
            new GrantFilter { SessionId = "sid-1" },
            revokedWhen,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(1, count);

        var session1 = await Store.GetOrDefaultAsync("grant-s1", CancellationToken.None);
        var session2 = await Store.GetOrDefaultAsync("grant-s2", CancellationToken.None);
        Assert.NotNull(session1);
        Assert.NotNull(session2);
        Assert.Equal(revokedWhen, session1.RevokedWhen);
        Assert.Null(session2.RevokedWhen);
    }

    [Fact]
    public async Task RevokeWhereAsync_WithoutSession_RevokesOnlyTheGrantsIssuedUnderNoSession()
    {
        await Store.AddAsync(
            CreateGrant(
                hashedKey: "hk-s1",
                grantId: "grant-s1",
                subjectId: "alice",
                sessionId: "sid-1"
            ),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-n1", grantId: "grant-n1", subjectId: "alice"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await Store.RevokeWhereAsync(
            new GrantFilter { SubjectId = "alice", WithoutSession = true },
            revokedWhen,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(1, count);

        var withSession = await Store.GetOrDefaultAsync("grant-s1", CancellationToken.None);
        var withoutSession = await Store.GetOrDefaultAsync("grant-n1", CancellationToken.None);
        Assert.NotNull(withSession);
        Assert.NotNull(withoutSession);
        Assert.Null(withSession.RevokedWhen);
        Assert.Equal(revokedWhen, withoutSession.RevokedWhen);
    }

    [Fact]
    public async Task RevokeWhereAsync_WhenFilteredByGrantId_RevokesOnlyThatGrant()
    {
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-g1", grantId: "grant-1", subjectId: "alice"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-g2", grantId: "grant-2", subjectId: "alice"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await Store.RevokeWhereAsync(
            new GrantFilter { GrantId = "grant-2" },
            revokedWhen,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(1, count);

        var grant1 = await Store.GetOrDefaultAsync("grant-1", CancellationToken.None);
        var grant2 = await Store.GetOrDefaultAsync("grant-2", CancellationToken.None);
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
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-f1", grantId: "grant-f1", refreshTokenLineageId: "fam-1"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-f2", grantId: "grant-f2", refreshTokenLineageId: "fam-1"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant(
                hashedKey: "hk-other",
                grantId: "grant-other",
                refreshTokenLineageId: "fam-2"
            ),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await Store.RevokeByRefreshTokenLineageAsync(
            "fam-1",
            revokedWhen,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(2, count);

        var family1 = await Store.GetOrDefaultAsync("grant-f1", CancellationToken.None);
        var family2 = await Store.GetOrDefaultAsync("grant-f2", CancellationToken.None);
        var other = await Store.GetOrDefaultAsync("grant-other", CancellationToken.None);
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
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-f", grantId: "grant-f", refreshTokenLineageId: "fam-1"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var first = await Store.RevokeByRefreshTokenLineageAsync(
            "fam-1",
            DateTimeOffset.UnixEpoch.AddMinutes(10),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var second = await Store.RevokeByRefreshTokenLineageAsync(
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

        await Store.AddAsync(
            CreateGrant(
                hashedKey: "hk-active",
                grantId: "grant-active",
                refreshTokenLineageId: "lineage-1"
            ),
            CancellationToken.None
        );
        await Store.AddAsync(revoked, CancellationToken.None);
        await Store.AddAsync(
            CreateGrant(
                hashedKey: "hk-other",
                grantId: "grant-other",
                refreshTokenLineageId: "lineage-2"
            ),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var grants = await Store.GetActiveByRefreshTokenLineageAsync(
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
        await Store.AddAsync(
            CreateGrant(hashedKey: "hk-f", grantId: "grant-f", refreshTokenLineageId: "lineage-1"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var grants = await Store.GetActiveByRefreshTokenLineageAsync(
            "lineage-1",
            CancellationToken.None
        );
        var grant = Assert.Single(grants);
        var concurrencyToken = grant.ConcurrencyToken;

        await Store.UpdateAsync(grant, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        var reloaded = await Store.GetOrDefaultAsync("grant-f", CancellationToken.None);

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

        await Store.AddAsync(expired, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveEndedBeforeAsync(cutoff, [], 10, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(1, removed);
        Assert.Null(await Store.GetOrDefaultAsync("grant-expired", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantWasRevokedBeforeTheCutoff_RemovesItBeforeItExpires()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var revoked = CreateGrant(hashedKey: "hk-revoked", grantId: "grant-revoked");
        revoked.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(30);
        revoked.RevokedWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await Store.AddAsync(revoked, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveEndedBeforeAsync(cutoff, [], 10, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(1, removed);
        Assert.Null(await Store.GetOrDefaultAsync("grant-revoked", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantHasNotEnded_KeepsIt()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var expiresLater = CreateGrant(hashedKey: "hk-later", grantId: "grant-later");
        expiresLater.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(20);
        var neverEnds = CreateGrant(hashedKey: "hk-never", grantId: "grant-never");
        neverEnds.ExpiresWhen = null;

        await Store.AddAsync(expiresLater, CancellationToken.None);
        await Store.AddAsync(neverEnds, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveEndedBeforeAsync(cutoff, [], 10, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(0, removed);
        Assert.NotNull(await Store.GetOrDefaultAsync("grant-later", CancellationToken.None));
        Assert.NotNull(await Store.GetOrDefaultAsync("grant-never", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_RemovesAtMostTheLimit()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        for (var index = 0; index < 3; ++index)
        {
            var ended = CreateGrant(hashedKey: $"hk-{index}", grantId: $"grant-{index}");
            ended.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
            await Store.AddAsync(ended, CancellationToken.None);
        }
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveEndedBeforeAsync(cutoff, [], 2, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(2, removed);
        Assert.Equal(1, await DbContext.Grants.CountAsync());
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenTheLimitIsLessThanOne_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await Store.RemoveEndedBeforeAsync(
                DateTimeOffset.UnixEpoch.AddDays(10),
                [],
                0,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantOfATypeEndedByRevocationExpiredUnrevoked_KeepsIt()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var expired = CreateGrant(hashedKey: "hk-expired", grantId: "grant-expired");
        expired.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);

        await Store.AddAsync(expired, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveEndedBeforeAsync(
            cutoff,
            [GrantType],
            10,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(0, removed);
        Assert.NotNull(await Store.GetOrDefaultAsync("grant-expired", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantOfATypeEndedByRevocationWasRevokedBeforeTheCutoff_RemovesIt()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var revoked = CreateGrant(hashedKey: "hk-revoked", grantId: "grant-revoked");
        revoked.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        revoked.RevokedWhen = DateTimeOffset.UnixEpoch.AddDays(2);

        await Store.AddAsync(revoked, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveEndedBeforeAsync(
            cutoff,
            [GrantType],
            10,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(1, removed);
        Assert.Null(await Store.GetOrDefaultAsync("grant-revoked", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEndedBeforeAsync_WhenAGrantOfATypeEndedByRevocationWasRevokedAfterTheCutoff_KeepsIt()
    {
        var cutoff = DateTimeOffset.UnixEpoch.AddDays(10);
        var revoked = CreateGrant(hashedKey: "hk-revoked", grantId: "grant-revoked");
        revoked.ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(1);
        revoked.RevokedWhen = DateTimeOffset.UnixEpoch.AddDays(20);

        await Store.AddAsync(revoked, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var removed = await Store.RemoveEndedBeforeAsync(
            cutoff,
            [GrantType],
            10,
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Equal(0, removed);
        Assert.NotNull(await Store.GetOrDefaultAsync("grant-revoked", CancellationToken.None));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ChangesConcurrencyToken()
    {
        await Store.AddAsync(CreateGrant(), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var current = await Store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);
        Assert.NotNull(current);

        current.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(5);
        await Store.UpdateAsync(current, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        var reloaded = await Store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(5), reloaded.ConsumedWhen);
    }

    [Fact]
    public async Task UpdateAsync_WhenTokenMismatch_Throws()
    {
        await Store.AddAsync(CreateGrant(), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var stale = CreateGrant();
        stale.ConcurrencyToken = "stale-token";
        stale.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(5);

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Store.UpdateAsync(stale, CancellationToken.None)
        );
    }

    #endregion

    #region Protected Payload Tests

    private static string[] PayloadPrefix { get; } = ["prot"];

    [Fact]
    public async Task GetProtectedPayloadPageAsync_ReturnsOnlyThePayloadsThatStartWithAPrefix()
    {
        await Store.AddAsync(
            CreateGrant("hash-1", "grant-1", payload: "aa-one"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant("hash-2", "grant-2", payload: "bb-two"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant("hash-3", "grant-3", payload: "cc-three"),
            CancellationToken.None
        );
        await Store.AddAsync(
            CreateGrant("hash-4", "grant-4", payload: "a"),
            CancellationToken.None
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetProtectedPayloadPageAsync(
            TenantId,
            ["aa", "bb"],
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(["grant-1", "grant-2"], page.Items.Select(x => x.RecordId).Order());
    }

    [Fact]
    public async Task GetProtectedPayloadPageAsync_WithPrefixesOfDifferentLengths_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Store
                .GetProtectedPayloadPageAsync(
                    TenantId,
                    ["aa", "b"],
                    new PageRequest { Limit = 10 },
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task GetProtectedPayloadPageAsync_ReturnsThePayloadsOfTheTenantOnly()
    {
        await Store.AddAsync(CreateGrant("hash-1", "grant-1"), CancellationToken.None);
        await Store.AddAsync(CreateGrant("hash-2", "grant-2"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var page = await Store.GetProtectedPayloadPageAsync(
            "TENANT-1",
            PayloadPrefix,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );
        var other = await Store.GetProtectedPayloadPageAsync(
            "tenant-2",
            PayloadPrefix,
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(["grant-1", "grant-2"], page.Items.Select(x => x.RecordId).Order());
        Assert.Equal(["hash-1", "hash-2"], page.Items.Select(x => x.ItemId).Order());
        Assert.All(page.Items, x => Assert.Equal("protected-payload", x.ProtectedValue));
        Assert.Empty(other.Items);
    }

    [Fact]
    public async Task GetProtectedPayloadPageAsync_PagesByCursor()
    {
        await Store.AddAsync(CreateGrant("hash-1", "grant-1"), CancellationToken.None);
        await Store.AddAsync(CreateGrant("hash-2", "grant-2"), CancellationToken.None);
        await Store.AddAsync(CreateGrant("hash-3", "grant-3"), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var first = await Store.GetProtectedPayloadPageAsync(
            TenantId,
            PayloadPrefix,
            new PageRequest { Limit = 2 },
            CancellationToken.None
        );
        var second = await Store.GetProtectedPayloadPageAsync(
            TenantId,
            PayloadPrefix,
            new PageRequest { Limit = 2, Cursor = first.NextCursor },
            CancellationToken.None
        );

        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task UpdateProtectedPayloadAsync_WritesThePayloadAndKeepsTheRestOfTheGrant()
    {
        await Store.AddAsync(CreateGrant(), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        var value = Assert.Single(
            (
                await Store.GetProtectedPayloadPageAsync(
                    TenantId,
                    PayloadPrefix,
                    new PageRequest { Limit = 10 },
                    CancellationToken.None
                )
            ).Items
        );
        value.ProtectedValue = "rewrapped-payload";

        await Store.UpdateProtectedPayloadAsync(value, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var reloaded = await Store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal("rewrapped-payload", reloaded.ProtectedPayload);
        Assert.Equal("subject-1", reloaded.SubjectId);
        Assert.NotEqual(value.ConcurrencyToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task UpdateProtectedPayloadAsync_WhenTheTokenIsStale_Throws()
    {
        await Store.AddAsync(CreateGrant(), CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await Assert.ThrowsAsync<StoreConcurrencyException>(() =>
            Store
                .UpdateProtectedPayloadAsync(
                    new PersistedProtectedValue
                    {
                        RecordId = "grant-1",
                        ItemId = HashedKey,
                        ProtectedValue = "x",
                        ConcurrencyToken = "stale",
                    },
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task UpdateProtectedPayloadAsync_WhenTheGrantIsMissing_ReturnsFalse()
    {
        var written = await Store.UpdateProtectedPayloadAsync(
            new PersistedProtectedValue
            {
                RecordId = "missing",
                ItemId = "x",
                ProtectedValue = "x",
                ConcurrencyToken = "x",
            },
            CancellationToken.None
        );

        Assert.False(written);
    }

    #endregion
}
