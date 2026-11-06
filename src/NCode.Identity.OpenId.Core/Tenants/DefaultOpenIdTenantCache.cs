// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Disposables;
using NCode.Identity.OpenId.Teams;
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

    // The cached tenant identifiers beneath each team at every level (compared without regard to case, as the team
    // cache keys them): a tenant is listed under its own team and under every team above it, so RemoveTeamAsync finds
    // the tenants of a team and of every team beneath it without scanning the cache. Each value holds the token of the
    // cache entry that registered the identifier, so an eviction callback removes only its own entry, and the
    // concurrency token of the team the tenant was materialized under, which GetTeamRevisionsAsync reports.
    // See ADR-1426.
    private ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, (object Entry, string TeamConcurrencyToken)>
    > TenantIdsByTeam { get; } = new(StringComparer.OrdinalIgnoreCase);

    // The row token of each cached tenant (compared ordinally, as the cache keys them), which GetRevisionsAsync
    // reports. Each value holds the token of the cache entry that registered it, so an eviction callback removes only
    // its own entry.
    private ConcurrentDictionary<
        string,
        (object Entry, string ConcurrencyToken)
    > RevisionsByTenantId { get; } = new(StringComparer.Ordinal);

    private static string GetCacheKey(string tenantId) =>
        $"NCode.Identity.OpenId.Tenants.DefaultOpenIdTenantCache:{tenantId}";

    private void EvictionCallback(object key, object? value, EvictionReason reason, object? state)
    {
        // The index entry is removed only while it still holds this cache entry's value, so a late callback of a
        // removed or expired entry never deletes its live replacement.
        if (
            state is ValueTuple<string, object, string, ImmutableArray<TeamRevision>>(
                var tenantId,
                var entry,
                var concurrencyToken,
                var lineage
            )
        )
        {
            RevisionsByTenantId.TryRemove(
                new KeyValuePair<string, (object Entry, string ConcurrencyToken)>(
                    tenantId,
                    (entry, concurrencyToken)
                )
            );

            foreach (var revision in lineage)
            {
                if (TenantIdsByTeam.TryGetValue(revision.TeamId, out var tenantIds))
                {
                    tenantIds.TryRemove(
                        new KeyValuePair<string, (object Entry, string TeamConcurrencyToken)>(
                            tenantId,
                            (entry, revision.ConcurrencyToken)
                        )
                    );
                }
            }
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
        var entry = new object();

        // The tenant's own team, then each team above it, because the tenant is materialized under all of them.
        var lineage = tenant.Value.Team.GetRevisionLineage();
        var concurrencyToken = tenant.Value.ConcurrencyToken;

        // The generation token, the index write and the cache write share one lock, so two concurrent calls for the
        // same tenant leave the index holding the value of the entry the cache holds, and a replaced entry's callback
        // never removes the survivor's index entry. The eviction callback does not take the lock.
        lock (Gate)
        {
            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Lifetime,
                ExpirationTokens = { new CancellationChangeToken(Generation.Token) },
                PostEvictionCallbacks =
                {
                    new PostEvictionCallbackRegistration
                    {
                        EvictionCallback = EvictionCallback,
                        State = (tenantId, entry, concurrencyToken, lineage),
                    },
                },
            };

            RevisionsByTenantId[tenantId] = (entry, concurrencyToken);

            foreach (var revision in lineage)
            {
                TenantIdsByTeam.GetOrAdd(
                    revision.TeamId,
                    _ => new ConcurrentDictionary<
                        string,
                        (object Entry, string TeamConcurrencyToken)
                    >()
                )[tenantId] = (entry, revision.ConcurrencyToken);
            }

            MemoryCache.Set(key, newLease, entryOptions);
        }

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
    public ValueTask<IReadOnlyCollection<TeamRevision>> GetTeamRevisionsAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<TeamRevision> revisions =
        [
            .. TenantIdsByTeam
                .SelectMany(pair =>
                    pair.Value.Values.Select(value => new TeamRevision(
                        pair.Key,
                        value.TeamConcurrencyToken
                    ))
                )
                .Distinct(),
        ];

        return ValueTask.FromResult(revisions);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<TenantRevision>> GetRevisionsAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<TenantRevision> revisions =
        [
            .. RevisionsByTenantId.Select(pair => new TenantRevision(
                pair.Key,
                pair.Value.ConcurrencyToken
            )),
        ];

        return ValueTask.FromResult(revisions);
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
