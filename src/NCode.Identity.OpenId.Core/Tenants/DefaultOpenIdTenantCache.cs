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

    // The cached tenant identifiers of each team (compared without regard to case, as the team cache keys them), so
    // RemoveTeamAsync finds a team's tenants without scanning the cache. Each value is the token of the cache entry
    // that registered the identifier, so an eviction callback removes only its own entry.
    private ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, object>
    > TenantIdsByTeam { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static string GetCacheKey(string tenantId) =>
        $"NCode.Identity.OpenId.Tenants.DefaultOpenIdTenantCache:{tenantId}";

    private void EvictionCallback(object key, object? value, EvictionReason reason, object? state)
    {
        // The index entry is removed only while it still holds this cache entry's token, so a late callback of a
        // removed or expired entry never deletes its live replacement.
        if (
            state is ValueTuple<string, string, object>(var teamId, var tenantId, var token)
            && TenantIdsByTeam.TryGetValue(teamId, out var tenantIds)
        )
        {
            tenantIds.TryRemove(new KeyValuePair<string, object>(tenantId, token));
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
        var token = new object();

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
                    State = (teamId, tenantId, token),
                },
            },
        };

        TenantIdsByTeam.GetOrAdd(teamId, _ => new ConcurrentDictionary<string, object>())[
            tenantId
        ] = token;
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
