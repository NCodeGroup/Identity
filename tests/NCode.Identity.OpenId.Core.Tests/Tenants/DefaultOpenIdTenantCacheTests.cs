#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

    public void Dispose() => MemoryCache.Dispose();

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenTheTenantIsCached_ReleasesTheCacheReferenceOnly()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockTenant = new Mock<OpenIdTenant>(MockBehavior.Strict);
        mockTenant
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Callback(disposed.SetResult)
            .Returns(ValueTask.CompletedTask);
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
}
