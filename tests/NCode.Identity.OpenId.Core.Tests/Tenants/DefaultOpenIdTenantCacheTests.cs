// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NCode.Disposables;
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
        string teamId = "platform"
    )
    {
        var mockTenant = new Mock<OpenIdTenant>(MockBehavior.Strict);
        mockTenant.SetupGet(x => x.TeamId).Returns(teamId);
        mockTenant
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Callback(() => disposed?.TrySetResult())
            .Returns(ValueTask.CompletedTask);
        return mockTenant;
    }

    private static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

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
