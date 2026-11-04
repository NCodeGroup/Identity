// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
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

    // The cached tenant identifiers of each team, so RemoveTeamAsync finds a team's tenants without scanning the cache.
    private ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, byte>
    > TenantIdsByTeam { get; } = new();

    private static string GetCacheKey(string tenantId) =>
        $"NCode.Identity.OpenId.Tenants.DefaultOpenIdTenantCache:{tenantId}";

    private void EvictionCallback(object key, object? value, EvictionReason reason, object? state)
    {
        // A replaced entry's identifier belongs to its replacement, which registered it already.
        if (
            reason != EvictionReason.Replaced
            && state is ValueTuple<string, string>(var teamId, var tenantId)
            && TenantIdsByTeam.TryGetValue(teamId, out var tenantIds)
        )
        {
            tenantIds.TryRemove(tenantId, out _);
        }

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
        var teamId = tenant.Value.TeamId;

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
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = EvictionCallback,
                    State = (teamId, tenantId),
                },
            },
        };

        TenantIdsByTeam.GetOrAdd(teamId, _ => new ConcurrentDictionary<string, byte>())[tenantId] =
            0;
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
    public async ValueTask RemoveTeamAsync(
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        if (!TenantIdsByTeam.TryGetValue(teamId, out var tenantIds))
            return;

        foreach (var tenantId in tenantIds.Keys)
        {
            await RemoveAsync(tenantId, propertyBag, cancellationToken);
        }
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
