#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Disposables;
using NCode.Identity.OpenId.Options;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdTenantCache"/> abstraction that uses <see cref="IMemoryCache"/>
/// with a sliding expiration to cache <see cref="OpenIdTenant"/> instances.
/// </summary>
public class DefaultOpenIdTenantCache(
    IOptions<OpenIdOptions> optionsAccessor,
    IMemoryCache memoryCache
) : IOpenIdTenantCache
{
    private IMemoryCache MemoryCache { get; } = memoryCache;

    private MemoryCacheEntryOptions MemoryCacheEntryOptions { get; } = new()
    {
        SlidingExpiration = optionsAccessor.Value.Tenant.TenantCacheExpiration,

        PostEvictionCallbacks =
        {
            new PostEvictionCallbackRegistration
            {
                EvictionCallback = EvictionCallback,
            }
        }
    };

    private static string GetCacheKey(TenantDescriptor tenantDescriptor) =>
        $"NCode.Identity.OpenId.Tenants.DefaultOpenIdTenantCache:{tenantDescriptor.TenantId}";

    private static void EvictionCallback(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is not IAsyncDisposable asyncDisposable) return;

        _ = Task.Factory.StartNew(
            DisposeCallbackAsync,
            asyncDisposable,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private static async Task DisposeCallbackAsync(object? state)
    {
        var asyncDisposable = (IAsyncDisposable?)state;
        if (asyncDisposable is null) return;
        await asyncDisposable.DisposeAsync();
    }

    /// <inheritdoc />
    public ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> TryGetAsync(
        TenantDescriptor tenantDescriptor,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        var key = GetCacheKey(tenantDescriptor);

        if (MemoryCache.TryGetValue<AsyncSharedReferenceLease<OpenIdTenant>>(key, out var existingLease) &&
            existingLease.TryAddReference(out var newLease))
        {
            return ValueTask.FromResult(newLease);
        }

        AsyncSharedReferenceLease<OpenIdTenant> empty = default;
        return ValueTask.FromResult(empty);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(
        TenantDescriptor tenantDescriptor,
        AsyncSharedReferenceLease<OpenIdTenant> tenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        var key = GetCacheKey(tenantDescriptor);
        var newLease = tenant.AddReference();

        MemoryCache.Set(key, newLease, MemoryCacheEntryOptions);

        return ValueTask.CompletedTask;
    }
}
