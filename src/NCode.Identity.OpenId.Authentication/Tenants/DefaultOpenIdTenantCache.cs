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
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdTenantCache"/> abstraction that uses <see cref="IMemoryCache"/>
/// with a sliding expiration to cache <see cref="OpenIdTenant"/> instances.
/// </summary>
internal class DefaultOpenIdTenantCache(
    IOptions<OpenIdOptions> optionsAccessor,
    IMemoryCache memoryCache
) : IOpenIdTenantCache
{
    private IMemoryCache MemoryCache { get; } = memoryCache;

    private MemoryCacheEntryOptions MemoryCacheEntryOptions { get; } =
        new()
        {
            SlidingExpiration = optionsAccessor.Value.Tenant.TenantCacheExpiration,

            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration { EvictionCallback = EvictionCallback },
            },
        };

    private static string GetCacheKey(string tenantId) =>
        $"NCode.Identity.OpenId.Tenants.DefaultOpenIdTenantCache:{tenantId}";

    private static void EvictionCallback(
        object key,
        object? value,
        EvictionReason reason,
        object? state
    )
    {
        if (value is not IAsyncDisposable asyncDisposable)
            return;

        // Offload disposal off the eviction thread; fire-and-forget.
        _ = Task.Run(async () => await asyncDisposable.DisposeAsync());
    }

    /// <inheritdoc />
    public ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> TryGetAsync(
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var key = GetCacheKey(tenantId);

        if (
            MemoryCache.TryGetValue<AsyncSharedReferenceLease<OpenIdTenant>>(
                key,
                out var existingLease
            ) && existingLease.TryAddReference(out var newLease)
        )
        {
            return ValueTask.FromResult(newLease);
        }

        AsyncSharedReferenceLease<OpenIdTenant> empty = default;
        return ValueTask.FromResult(empty);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(
        string tenantId,
        AsyncSharedReferenceLease<OpenIdTenant> tenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var key = GetCacheKey(tenantId);
        var newLease = tenant.AddReference();

        MemoryCache.Set(key, newLease, MemoryCacheEntryOptions);

        return ValueTask.CompletedTask;
    }
}
