// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NCode.Disposables;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultOpenIdTenantCacheTests : IDisposable
{
    private const string TenantId = "acme";

    private MemoryCache MemoryCache { get; } = new(new MemoryCacheOptions());
    private DefaultOpenIdTenantCache Cache { get; }

    public DefaultOpenIdTenantCacheTests()
    {
        Cache = new DefaultOpenIdTenantCache(Options.Create(new OpenIdOptions()), MemoryCache);
    }

    public void Dispose()
    {
        Cache.Dispose();
        MemoryCache.Dispose();
    }

    private static Mock<OpenIdTenant> CreateMockTenant(
        TaskCompletionSource? disposed = null,
        string teamId = "platform",
        string teamConcurrencyToken = "team-ct",
        Mock<OpenIdTeam>? parentTeam = null,
        string concurrencyToken = "tenant-ct"
    )
    {
        var mockTeam = new Mock<OpenIdTeam>(MockBehavior.Strict);
        mockTeam.SetupGet(x => x.TeamId).Returns(teamId).Verifiable();
        mockTeam.SetupGet(x => x.ConcurrencyToken).Returns(teamConcurrencyToken).Verifiable();
        mockTeam.SetupGet(x => x.Parent).Returns(parentTeam?.Object).Verifiable();

        var mockTenant = new Mock<OpenIdTenant>(MockBehavior.Strict);
        mockTenant.SetupGet(x => x.TeamId).Returns(teamId).Verifiable();
        mockTenant.SetupGet(x => x.Team).Returns(mockTeam.Object).Verifiable();
        mockTenant.SetupGet(x => x.ConcurrencyToken).Returns(concurrencyToken).Verifiable();
        mockTenant
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Callback(() => disposed?.TrySetResult())
            .Returns(ValueTask.CompletedTask);
        return mockTenant;
    }

    private static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Mock<OpenIdTeam> CreateMockParentTeam(string teamId, string concurrencyToken)
    {
        var mockParent = new Mock<OpenIdTeam>(MockBehavior.Strict);
        mockParent.SetupGet(x => x.TeamId).Returns(teamId).Verifiable();
        mockParent.SetupGet(x => x.ConcurrencyToken).Returns(concurrencyToken).Verifiable();
        mockParent.SetupGet(x => x.Parent).Returns((OpenIdTeam?)null).Verifiable();
        return mockParent;
    }

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenTheTenantIsCached_ReleasesTheCacheReferenceOnly()
    {
        var disposed = CreateCompletionSource();
        var mockTenant = CreateMockTenant(disposed);
        var propertyBag = PropertyBagFactory.Create();

        // Only the cache and the handed-out lease hold the tenant once the creator releases its own reference.
        AsyncSharedReferenceLease<OpenIdTenant> handedOut;
        await using (var owner = AsyncSharedReference.Create(mockTenant.Object))
        {
            await Cache.SetAsync(TenantId, owner, propertyBag, CancellationToken.None);
            handedOut = await Cache.TryGetAsync(TenantId, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveAsync(TenantId, propertyBag, CancellationToken.None);

        await using (
            var afterRemoval = await Cache.TryGetAsync(
                TenantId,
                propertyBag,
                CancellationToken.None
            )
        )
        {
            Assert.False(afterRemoval.IsActive);
        }

        // The eviction releases the cache's reference off the calling thread; the handed-out lease keeps the tenant.
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        Assert.False(disposed.Task.IsCompleted);
        Assert.Same(mockTenant.Object, handedOut.Value);

        // Releasing the last lease disposes the tenant, so the eviction released exactly the cache's reference.
        await handedOut.DisposeAsync();
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTenantIsNotCached_DoesNothing()
    {
        var propertyBag = PropertyBagFactory.Create();

        await Cache.RemoveAsync(TenantId, propertyBag, CancellationToken.None);

        await using var lease = await Cache.TryGetAsync(
            TenantId,
            propertyBag,
            CancellationToken.None
        );
        Assert.False(lease.IsActive);
    }

    #endregion

    #region RemoveTeamAsync Tests

    [Fact]
    public async Task RemoveTeamAsync_EvictsOnlyTheTeamsTenants()
    {
        const string otherTenantId = "globex";
        const string thirdTenantId = "initech";
        var firstDisposed = CreateCompletionSource();
        var secondDisposed = CreateCompletionSource();
        var firstTenant = CreateMockTenant(firstDisposed, "team-a");
        var secondTenant = CreateMockTenant(secondDisposed, "team-a");
        var thirdTenant = CreateMockTenant(teamId: "team-b");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        await using (var third = AsyncSharedReference.Create(thirdTenant.Object))
        {
            await Cache.SetAsync(TenantId, first, propertyBag, CancellationToken.None);
            await Cache.SetAsync(otherTenantId, second, propertyBag, CancellationToken.None);
            await Cache.SetAsync(thirdTenantId, third, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveTeamAsync("team-a", propertyBag, CancellationToken.None);

        foreach (var evictedId in new[] { TenantId, otherTenantId })
        {
            await using var evicted = await Cache.TryGetAsync(
                evictedId,
                propertyBag,
                CancellationToken.None
            );
            Assert.False(evicted.IsActive);
        }

        await using (
            var kept = await Cache.TryGetAsync(thirdTenantId, propertyBag, CancellationToken.None)
        )
        {
            Assert.True(kept.IsActive);
            Assert.Same(thirdTenant.Object, kept.Value);
        }

        await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await secondDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task RemoveTeamAsync_WithAnotherCasing_EvictsTheTeamsTenants()
    {
        var disposed = CreateCompletionSource();
        var mockTenant = CreateMockTenant(disposed, "Team-A");
        var propertyBag = PropertyBagFactory.Create();

        await using (var owner = AsyncSharedReference.Create(mockTenant.Object))
        {
            await Cache.SetAsync(TenantId, owner, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveTeamAsync("team-a", propertyBag, CancellationToken.None);

        await using (
            var evicted = await Cache.TryGetAsync(TenantId, propertyBag, CancellationToken.None)
        )
        {
            Assert.False(evicted.IsActive);
        }

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task RemoveTeamAsync_WithATenantOfAChildTeam_RemovesIt()
    {
        var disposed = CreateCompletionSource();
        var parentTeam = CreateMockParentTeam("team-a", "a1");
        var childTenant = CreateMockTenant(disposed, "team-a-web", parentTeam: parentTeam);
        var propertyBag = PropertyBagFactory.Create();

        await using (var owner = AsyncSharedReference.Create(childTenant.Object))
        {
            await Cache.SetAsync(TenantId, owner, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveTeamAsync("team-a", propertyBag, CancellationToken.None);

        await using (
            var evicted = await Cache.TryGetAsync(TenantId, propertyBag, CancellationToken.None)
        )
        {
            Assert.False(evicted.IsActive);
        }

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task SetAsync_AfterRemoveAsync_KeepsTheReplacementInTheTeamIndex()
    {
        var firstDisposed = CreateCompletionSource();
        var firstTenant = CreateMockTenant(firstDisposed, "team-a");
        var secondDisposed = CreateCompletionSource();
        var secondTenant = CreateMockTenant(secondDisposed, "team-a");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        {
            await Cache.SetAsync(TenantId, first, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveAsync(TenantId, propertyBag, CancellationToken.None);

        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        {
            await Cache.SetAsync(TenantId, second, propertyBag, CancellationToken.None);
        }

        // The first entry's eviction callback has run once its lease is disposed; it must not drop the replacement.
        await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        await Cache.RemoveTeamAsync("team-a", propertyBag, CancellationToken.None);

        await using (
            var evicted = await Cache.TryGetAsync(TenantId, propertyBag, CancellationToken.None)
        )
        {
            Assert.False(evicted.IsActive);
        }

        await secondDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task SetAsync_WhenTwoMaterializationsRace_KeepsTheCachedEntryInTheTeamIndex()
    {
        var propertyBag = PropertyBagFactory.Create();

        for (var iteration = 0; iteration < 200; iteration++)
        {
            var tenantId = $"race-{iteration}";
            var firstDisposed = CreateCompletionSource();
            var firstTenant = CreateMockTenant(firstDisposed, "team-a");
            var secondDisposed = CreateCompletionSource();
            var secondTenant = CreateMockTenant(secondDisposed, "team-a");
            var start = CreateCompletionSource();

            await using (var first = AsyncSharedReference.Create(firstTenant.Object))
            await using (var second = AsyncSharedReference.Create(secondTenant.Object))
            {
                var firstSet = Task.Run(async () =>
                {
                    await start.Task;
                    await Cache.SetAsync(tenantId, first, propertyBag, CancellationToken.None);
                });
                var secondSet = Task.Run(async () =>
                {
                    await start.Task;
                    await Cache.SetAsync(tenantId, second, propertyBag, CancellationToken.None);
                });

                start.SetResult();
                await Task.WhenAll(firstSet, secondSet)
                    .WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            }

            // Exactly one entry was replaced; its lease is disposed once its eviction callback has run.
            await Task.WhenAny(firstDisposed.Task, secondDisposed.Task)
                .WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

            await Cache.RemoveTeamAsync("team-a", propertyBag, CancellationToken.None);

            await using (
                var evicted = await Cache.TryGetAsync(tenantId, propertyBag, CancellationToken.None)
            )
            {
                Assert.False(evicted.IsActive);
            }

            await Task.WhenAll(firstDisposed.Task, secondDisposed.Task)
                .WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
    }

    #endregion

    #region GetTeamRevisionsAsync Tests

    [Fact]
    public async Task GetTeamRevisionsAsync_ReturnsEachTeamRevisionOnce()
    {
        var firstTenant = CreateMockTenant(teamId: "team-a", teamConcurrencyToken: "a1");
        var secondTenant = CreateMockTenant(teamId: "team-a", teamConcurrencyToken: "a1");
        var thirdTenant = CreateMockTenant(teamId: "team-b", teamConcurrencyToken: "b1");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        await using (var third = AsyncSharedReference.Create(thirdTenant.Object))
        {
            await Cache.SetAsync("acme", first, propertyBag, CancellationToken.None);
            await Cache.SetAsync("globex", second, propertyBag, CancellationToken.None);
            await Cache.SetAsync("initech", third, propertyBag, CancellationToken.None);
        }

        var revisions = await Cache.GetTeamRevisionsAsync(propertyBag, CancellationToken.None);

        Assert.Equal(2, revisions.Count);
        Assert.Contains(new TeamRevision("team-a", "a1"), revisions);
        Assert.Contains(new TeamRevision("team-b", "b1"), revisions);
    }

    [Fact]
    public async Task GetTeamRevisionsAsync_WhenATeamsTenantsWereMaterializedUnderTwoRevisions_ReturnsBoth()
    {
        var firstTenant = CreateMockTenant(teamId: "team-a", teamConcurrencyToken: "a1");
        var secondTenant = CreateMockTenant(teamId: "team-a", teamConcurrencyToken: "a2");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        {
            await Cache.SetAsync("acme", first, propertyBag, CancellationToken.None);
            await Cache.SetAsync("globex", second, propertyBag, CancellationToken.None);
        }

        var revisions = await Cache.GetTeamRevisionsAsync(propertyBag, CancellationToken.None);

        Assert.Equal(2, revisions.Count);
        Assert.Contains(new TeamRevision("team-a", "a1"), revisions);
        Assert.Contains(new TeamRevision("team-a", "a2"), revisions);
    }

    [Fact]
    public async Task GetTeamRevisionsAsync_AfterRemoveTeamAsync_OmitsTheTeam()
    {
        var firstTenant = CreateMockTenant(teamId: "team-a", teamConcurrencyToken: "a1");
        var secondTenant = CreateMockTenant(teamId: "team-b", teamConcurrencyToken: "b1");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        {
            await Cache.SetAsync("acme", first, propertyBag, CancellationToken.None);
            await Cache.SetAsync("globex", second, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveTeamAsync("team-a", propertyBag, CancellationToken.None);

        // The eviction callback runs off the calling thread.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            var revisions = await Cache.GetTeamRevisionsAsync(propertyBag, CancellationToken.None);

            if (revisions.Count == 1)
            {
                Assert.Equal(new TeamRevision("team-b", "b1"), Assert.Single(revisions));
                break;
            }

            Assert.True(
                DateTime.UtcNow < deadline,
                "The team revision was not removed within 5 seconds."
            );

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetTeamRevisionsAsync_WithATenantOfAChildTeam_ReportsTheParentsRevision()
    {
        var parentTeam = CreateMockParentTeam("team-a", "a1");
        var childTenant = CreateMockTenant(
            teamId: "team-a-web",
            teamConcurrencyToken: "w1",
            parentTeam: parentTeam
        );
        var propertyBag = PropertyBagFactory.Create();

        await using (var child = AsyncSharedReference.Create(childTenant.Object))
        {
            await Cache.SetAsync("acme", child, propertyBag, CancellationToken.None);
        }

        var revisions = await Cache.GetTeamRevisionsAsync(propertyBag, CancellationToken.None);

        Assert.Equal(2, revisions.Count);
        Assert.Contains(new TeamRevision("team-a-web", "w1"), revisions);
        Assert.Contains(new TeamRevision("team-a", "a1"), revisions);
    }

    #endregion

    #region GetRevisionsAsync Tests

    [Fact]
    public async Task GetRevisionsAsync_ReturnsTheRevisionOfEachCachedTenant()
    {
        var firstTenant = CreateMockTenant(concurrencyToken: "t1");
        var secondTenant = CreateMockTenant(concurrencyToken: "t2");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        {
            await Cache.SetAsync("acme", first, propertyBag, CancellationToken.None);
            await Cache.SetAsync("globex", second, propertyBag, CancellationToken.None);
        }

        var revisions = await Cache.GetRevisionsAsync(propertyBag, CancellationToken.None);

        Assert.Equal(2, revisions.Count);
        Assert.Contains(new TenantRevision("acme", "t1"), revisions);
        Assert.Contains(new TenantRevision("globex", "t2"), revisions);
    }

    [Fact]
    public async Task GetRevisionsAsync_AfterRemoveAsync_OmitsTheTenant()
    {
        var firstDisposed = CreateCompletionSource();
        var firstTenant = CreateMockTenant(firstDisposed, concurrencyToken: "t1");
        var secondTenant = CreateMockTenant(concurrencyToken: "t2");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        {
            await Cache.SetAsync("acme", first, propertyBag, CancellationToken.None);
            await Cache.SetAsync("globex", second, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveAsync("acme", propertyBag, CancellationToken.None);

        // The eviction callback has run once the removed tenant is disposed.
        await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        var revisions = await Cache.GetRevisionsAsync(propertyBag, CancellationToken.None);

        Assert.Equal(new TenantRevision("globex", "t2"), Assert.Single(revisions));
    }

    [Fact]
    public async Task GetRevisionsAsync_WhenATenantIsCachedAgain_ReportsOnlyTheLatestRevision()
    {
        var firstDisposed = CreateCompletionSource();
        var firstTenant = CreateMockTenant(firstDisposed, concurrencyToken: "t1");
        var secondTenant = CreateMockTenant(concurrencyToken: "t2");
        var propertyBag = PropertyBagFactory.Create();

        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        {
            await Cache.SetAsync(TenantId, first, propertyBag, CancellationToken.None);
        }

        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        {
            await Cache.SetAsync(TenantId, second, propertyBag, CancellationToken.None);
        }

        // The first entry's eviction callback has run once its lease is disposed.
        await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        var revisions = await Cache.GetRevisionsAsync(propertyBag, CancellationToken.None);

        Assert.Equal(new TenantRevision(TenantId, "t2"), Assert.Single(revisions));
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_EvictsEveryCachedTenant()
    {
        const string otherTenantId = "globex";
        var firstDisposed = CreateCompletionSource();
        var secondDisposed = CreateCompletionSource();
        var firstTenant = CreateMockTenant(firstDisposed);
        var secondTenant = CreateMockTenant(secondDisposed);
        var propertyBag = PropertyBagFactory.Create();

        // Only the cache holds the tenants once the creators release their own references.
        await using (var first = AsyncSharedReference.Create(firstTenant.Object))
        await using (var second = AsyncSharedReference.Create(secondTenant.Object))
        {
            await Cache.SetAsync(TenantId, first, propertyBag, CancellationToken.None);
            await Cache.SetAsync(otherTenantId, second, propertyBag, CancellationToken.None);
        }

        await Cache.ClearAsync(propertyBag, CancellationToken.None);

        await using (
            var firstAfterClear = await Cache.TryGetAsync(
                TenantId,
                propertyBag,
                CancellationToken.None
            )
        )
        {
            Assert.False(firstAfterClear.IsActive);
        }

        await using (
            var secondAfterClear = await Cache.TryGetAsync(
                otherTenantId,
                propertyBag,
                CancellationToken.None
            )
        )
        {
            Assert.False(secondAfterClear.IsActive);
        }

        // The eviction releases the cache's references, so each tenant is disposed.
        await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await secondDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task ClearAsync_EmptiesTheTeamIndex()
    {
        const string otherTenantId = "globex";
        var clearedDisposed = CreateCompletionSource();
        var clearedTenant = CreateMockTenant(clearedDisposed, "team-a");
        var replacementDisposed = CreateCompletionSource();
        var replacementTenant = CreateMockTenant(replacementDisposed, "team-a");
        var otherTenant = CreateMockTenant(teamId: "team-b");
        var propertyBag = PropertyBagFactory.Create();

        await using (var owner = AsyncSharedReference.Create(clearedTenant.Object))
        {
            await Cache.SetAsync(TenantId, owner, propertyBag, CancellationToken.None);
        }

        await Cache.ClearAsync(propertyBag, CancellationToken.None);

        // The eviction callbacks run off the calling thread; the disposal follows the callback.
        await clearedDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        await using (var owner = AsyncSharedReference.Create(replacementTenant.Object))
        await using (var other = AsyncSharedReference.Create(otherTenant.Object))
        {
            await Cache.SetAsync(TenantId, owner, propertyBag, CancellationToken.None);
            await Cache.SetAsync(otherTenantId, other, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveTeamAsync("team-a", propertyBag, CancellationToken.None);

        await using (
            var evicted = await Cache.TryGetAsync(TenantId, propertyBag, CancellationToken.None)
        )
        {
            Assert.False(evicted.IsActive);
        }

        await replacementDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        await using var kept = await Cache.TryGetAsync(
            otherTenantId,
            propertyBag,
            CancellationToken.None
        );
        Assert.True(kept.IsActive);
        Assert.Same(otherTenant.Object, kept.Value);
    }

    [Fact]
    public async Task ClearAsync_KeepsATenantCachedAfterIt()
    {
        var mockTenant = CreateMockTenant();
        var propertyBag = PropertyBagFactory.Create();

        await Cache.ClearAsync(propertyBag, CancellationToken.None);

        await using var owner = AsyncSharedReference.Create(mockTenant.Object);
        await Cache.SetAsync(TenantId, owner, propertyBag, CancellationToken.None);

        await using var lease = await Cache.TryGetAsync(
            TenantId,
            propertyBag,
            CancellationToken.None
        );

        Assert.True(lease.IsActive);
        Assert.Same(mockTenant.Object, lease.Value);
    }

    #endregion

    #region SetAsync Tests

    [Fact]
    public async Task SetAsync_WhenTheLifetimeElapses_EvictsTheTenant()
    {
        var mockTenant = CreateMockTenant();
        var propertyBag = PropertyBagFactory.Create();
        var options = new OpenIdOptions
        {
            Tenant = { TenantCacheLifetime = TimeSpan.FromMilliseconds(50) },
        };

        using var shortLivedMemoryCache = new MemoryCache(new MemoryCacheOptions());
        using var shortLivedCache = new DefaultOpenIdTenantCache(
            Options.Create(options),
            shortLivedMemoryCache
        );

        await using (var owner = AsyncSharedReference.Create(mockTenant.Object))
        {
            await shortLivedCache.SetAsync(TenantId, owner, propertyBag, CancellationToken.None);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            await using var lease = await shortLivedCache.TryGetAsync(
                TenantId,
                propertyBag,
                CancellationToken.None
            );

            if (!lease.IsActive)
                break;

            Assert.True(DateTime.UtcNow < deadline, "The tenant was not evicted within 5 seconds.");

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken.None);
        }
    }

    #endregion
}
