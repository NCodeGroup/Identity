// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NCode.Disposables;
using NCode.Identity.OpenId.Teams;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class DefaultOpenIdTeamCacheTests : IDisposable
{
    private const string TeamId = "acme";

    private MemoryCache MemoryCache { get; } = new(new MemoryCacheOptions());
    private DefaultOpenIdTeamCache Cache { get; }

    public DefaultOpenIdTeamCacheTests()
    {
        Cache = new DefaultOpenIdTeamCache(Options.Create(new OpenIdOptions()), MemoryCache);
    }

    public void Dispose()
    {
        Cache.Dispose();
        MemoryCache.Dispose();
    }

    private static Mock<OpenIdTeam> CreateMockTeam(TaskCompletionSource? disposed = null)
    {
        var mockTeam = new Mock<OpenIdTeam>(MockBehavior.Strict);
        mockTeam
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Callback(() => disposed?.TrySetResult())
            .Returns(ValueTask.CompletedTask);
        return mockTeam;
    }

    private static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    #region TryGetAsync Tests

    [Fact]
    public async Task TryGetAsync_AfterSetAsync_ReturnsTheCachedTeam()
    {
        var mockTeam = CreateMockTeam();
        var propertyBag = PropertyBagFactory.Create();

        await using var owner = AsyncSharedReference.Create(mockTeam.Object);
        await Cache.SetAsync(TeamId, owner, propertyBag, CancellationToken.None);

        await using var lease = await Cache.TryGetAsync(
            TeamId,
            propertyBag,
            CancellationToken.None
        );

        Assert.True(lease.IsActive);
        Assert.Same(mockTeam.Object, lease.Value);
    }

    [Fact]
    public async Task TryGetAsync_WhenTheTeamIsNotCached_ReturnsAnInactiveLease()
    {
        var propertyBag = PropertyBagFactory.Create();

        await using var lease = await Cache.TryGetAsync(
            TeamId,
            propertyBag,
            CancellationToken.None
        );

        Assert.False(lease.IsActive);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenTheTeamIsCached_ReleasesTheCacheReferenceOnly()
    {
        var disposed = CreateCompletionSource();
        var mockTeam = CreateMockTeam(disposed);
        var propertyBag = PropertyBagFactory.Create();

        // Only the cache and the handed-out lease hold the team once the creator releases its own reference.
        AsyncSharedReferenceLease<OpenIdTeam> handedOut;
        await using (var owner = AsyncSharedReference.Create(mockTeam.Object))
        {
            await Cache.SetAsync(TeamId, owner, propertyBag, CancellationToken.None);
            handedOut = await Cache.TryGetAsync(TeamId, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveAsync(TeamId, propertyBag, CancellationToken.None);

        await using (
            var afterRemoval = await Cache.TryGetAsync(TeamId, propertyBag, CancellationToken.None)
        )
        {
            Assert.False(afterRemoval.IsActive);
        }

        // The eviction releases the cache's reference off the calling thread; the handed-out lease keeps the team.
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        Assert.False(disposed.Task.IsCompleted);
        Assert.Same(mockTeam.Object, handedOut.Value);

        // Releasing the last lease disposes the team, so the eviction released exactly the cache's reference.
        await handedOut.DisposeAsync();
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task RemoveAsync_WithAnotherCasing_EvictsTheTeam()
    {
        var disposed = CreateCompletionSource();
        var mockTeam = CreateMockTeam(disposed);
        var propertyBag = PropertyBagFactory.Create();

        await using (var owner = AsyncSharedReference.Create(mockTeam.Object))
        {
            await Cache.SetAsync(TeamId, owner, propertyBag, CancellationToken.None);
        }

        await Cache.RemoveAsync("ACME", propertyBag, CancellationToken.None);

        await using (
            var afterRemoval = await Cache.TryGetAsync(TeamId, propertyBag, CancellationToken.None)
        )
        {
            Assert.False(afterRemoval.IsActive);
        }

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheTeamIsNotCached_DoesNothing()
    {
        var propertyBag = PropertyBagFactory.Create();

        await Cache.RemoveAsync(TeamId, propertyBag, CancellationToken.None);

        await using var lease = await Cache.TryGetAsync(
            TeamId,
            propertyBag,
            CancellationToken.None
        );
        Assert.False(lease.IsActive);
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_EvictsEveryCachedTeam()
    {
        const string otherTeamId = "globex";
        var firstDisposed = CreateCompletionSource();
        var secondDisposed = CreateCompletionSource();
        var firstTeam = CreateMockTeam(firstDisposed);
        var secondTeam = CreateMockTeam(secondDisposed);
        var propertyBag = PropertyBagFactory.Create();

        // Only the cache holds the teams once the creators release their own references.
        await using (var first = AsyncSharedReference.Create(firstTeam.Object))
        await using (var second = AsyncSharedReference.Create(secondTeam.Object))
        {
            await Cache.SetAsync(TeamId, first, propertyBag, CancellationToken.None);
            await Cache.SetAsync(otherTeamId, second, propertyBag, CancellationToken.None);
        }

        await Cache.ClearAsync(propertyBag, CancellationToken.None);

        foreach (var evictedId in new[] { TeamId, otherTeamId })
        {
            await using var evicted = await Cache.TryGetAsync(
                evictedId,
                propertyBag,
                CancellationToken.None
            );
            Assert.False(evicted.IsActive);
        }

        // The eviction releases the cache's references, so each team is disposed.
        await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await secondDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
    }

    [Fact]
    public async Task ClearAsync_KeepsATeamCachedAfterIt()
    {
        var mockTeam = CreateMockTeam();
        var propertyBag = PropertyBagFactory.Create();

        await Cache.ClearAsync(propertyBag, CancellationToken.None);

        await using var owner = AsyncSharedReference.Create(mockTeam.Object);
        await Cache.SetAsync(TeamId, owner, propertyBag, CancellationToken.None);

        await using var lease = await Cache.TryGetAsync(
            TeamId,
            propertyBag,
            CancellationToken.None
        );

        Assert.True(lease.IsActive);
        Assert.Same(mockTeam.Object, lease.Value);
    }

    #endregion

    #region SetAsync Tests

    [Fact]
    public async Task SetAsync_WhenTheLifetimeElapses_EvictsTheTeam()
    {
        var mockTeam = CreateMockTeam();
        var propertyBag = PropertyBagFactory.Create();
        var options = new OpenIdOptions
        {
            Team = { TeamCacheLifetime = TimeSpan.FromMilliseconds(50) },
        };

        using var shortLivedMemoryCache = new MemoryCache(new MemoryCacheOptions());
        using var shortLivedCache = new DefaultOpenIdTeamCache(
            Options.Create(options),
            shortLivedMemoryCache
        );

        await using (var owner = AsyncSharedReference.Create(mockTeam.Object))
        {
            await shortLivedCache.SetAsync(TeamId, owner, propertyBag, CancellationToken.None);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            await using var lease = await shortLivedCache.TryGetAsync(
                TeamId,
                propertyBag,
                CancellationToken.None
            );

            if (!lease.IsActive)
                break;

            Assert.True(DateTime.UtcNow < deadline, "The team was not evicted within 5 seconds.");

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken.None);
        }
    }

    [Fact]
    public async Task SetAsync_ReadsTheLifetimeFromTheOptionsOnUse()
    {
        var mockTeam = CreateMockTeam();
        var propertyBag = PropertyBagFactory.Create();
        var options = new OpenIdOptions();

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var cache = new DefaultOpenIdTeamCache(Options.Create(options), memoryCache);

        // The lifetime changes after construction, so only a lifetime read on use evicts the team.
        options.Team.TeamCacheLifetime = TimeSpan.FromMilliseconds(50);

        await using (var owner = AsyncSharedReference.Create(mockTeam.Object))
        {
            await cache.SetAsync(TeamId, owner, propertyBag, CancellationToken.None);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            await using var lease = await cache.TryGetAsync(
                TeamId,
                propertyBag,
                CancellationToken.None
            );

            if (!lease.IsActive)
                break;

            Assert.True(DateTime.UtcNow < deadline, "The team was not evicted within 5 seconds.");

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken.None);
        }
    }

    #endregion
}
