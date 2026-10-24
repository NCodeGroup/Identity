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
using Microsoft.Extensions.Primitives;
using NCode.Disposables;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdTenantCache"/> abstraction that uses <see cref="IMemoryCache"/>
/// with an absolute lifetime to cache <see cref="OpenIdTenant"/> instances. Each tenant is cached for at most
/// <see cref="OpenIdTenantOptions.TenantCacheLifetime"/>, or until <see cref="ClearAsync"/> evicts every cached tenant.
/// </summary>
internal class DefaultOpenIdTenantCache(
    IOptions<OpenIdOptions> optionsAccessor,
    IMemoryCache memoryCache
) : IOpenIdTenantCache, IDisposable
{
    private IMemoryCache MemoryCache { get; } = memoryCache;

    private TimeSpan Lifetime { get; } = optionsAccessor.Value.Tenant.TenantCacheLifetime;

    private Lock Gate { get; } = new();

    // Cancelled by ClearAsync, so every entry cached before the call expires.
    private CancellationTokenSource Generation { get; set; } = new();

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

        CancellationToken generation;
        lock (Gate)
        {
            generation = Generation.Token;
        }

        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Lifetime,
            ExpirationTokens = { new CancellationChangeToken(generation) },
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration { EvictionCallback = EvictionCallback },
            },
        };

        MemoryCache.Set(key, newLease, entryOptions);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // The eviction callback releases the cache's reference; a lease already handed out keeps the tenant alive.
        MemoryCache.Remove(GetCacheKey(tenantId));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(IPropertyBag propertyBag, CancellationToken cancellationToken)
    {
        CancellationTokenSource previous;
        lock (Gate)
        {
            previous = Generation;
            Generation = new CancellationTokenSource();
        }

        // The eviction callbacks release the cache's references; a lease already handed out keeps its tenant alive.
        previous.Cancel();
        previous.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        lock (Gate)
        {
            Generation.Dispose();
        }
    }
}
