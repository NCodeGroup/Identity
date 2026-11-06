// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Disposables;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdTeamCache"/> abstraction that uses <see cref="IMemoryCache"/>
/// with an absolute lifetime to cache <see cref="OpenIdTeam"/> instances. Each team is cached for at most
/// <see cref="OpenIdTeamOptions.TeamCacheLifetime"/>, or until <see cref="ClearAsync"/> evicts every cached team.
/// <see cref="RemoveAsync"/> removes a team and every cached team beneath it, because a team is materialized over its
/// parent. An index of the cached teams' <see cref="TeamRevision"/> values, and of the revisions of the teams above
/// them, answers <see cref="GetRevisionsAsync"/> without scanning the cache.
/// </summary>
// See ADR-1411, ADR-1416 and ADR-1426.
internal class DefaultOpenIdTeamCache(
    IOptions<OpenIdOptions> optionsAccessor,
    IMemoryCache memoryCache
) : IOpenIdTeamCache, IDisposable
{
    private IMemoryCache MemoryCache { get; } = memoryCache;

    // Read on use, never in the constructor, so the lifetime follows the options (ADR-1204).
    private TimeSpan Lifetime => optionsAccessor.Value.Team.TeamCacheLifetime;

    private Lock Gate { get; } = new();

    // Cancelled by ClearAsync, so every entry cached before the call expires.
    private CancellationTokenSource Generation { get; set; } = new();

    // The lineage of each cached team (compared without regard to case, as the cache keys them): the team's own
    // revision followed by the revision of each team above it, so GetRevisionsAsync lists the cached teams without
    // scanning the cache. Each value holds the token of the cache entry that registered it, so an eviction callback
    // removes only its own entry.
    private ConcurrentDictionary<
        string,
        (object Entry, ImmutableArray<TeamRevision> Lineage)
    > CachedRevisions { get; } = new(StringComparer.OrdinalIgnoreCase);

    // The cached team identifiers beneath each team, so RemoveAsync finds a team's descendants without scanning the
    // cache. Each value holds the token of the cache entry that registered the identifier, so an eviction callback
    // removes only its own entry.
    private ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, object>
    > TeamIdsByAncestor { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Team identifiers compare without regard to case, so the key is the lowercase identifier.
    private static string GetCacheKey(string teamId) =>
        $"NCode.Identity.OpenId.Teams.DefaultOpenIdTeamCache:{teamId.ToLowerInvariant()}";

    private void EvictionCallback(object key, object? value, EvictionReason reason, object? state)
    {
        // The index entry is removed only while it still holds this cache entry, so a late callback of a removed or
        // expired entry never deletes its live replacement.
        if (
            state is ValueTuple<string, ValueTuple<object, ImmutableArray<TeamRevision>>>(
                var teamId,
                var cached
            )
        )
        {
            CachedRevisions.TryRemove(
                new KeyValuePair<string, (object Entry, ImmutableArray<TeamRevision> Lineage)>(
                    teamId,
                    cached
                )
            );

            foreach (var ancestor in cached.Item2.Skip(1))
            {
                if (TeamIdsByAncestor.TryGetValue(ancestor.TeamId, out var teamIds))
                {
                    teamIds.TryRemove(new KeyValuePair<string, object>(teamId, cached.Item1));
                }
            }
        }

        if (value is not IAsyncDisposable asyncDisposable)
            return;

        // Offload disposal off the eviction thread; fire-and-forget.
        _ = Task.Run(async () => await asyncDisposable.DisposeAsync());
    }

    /// <inheritdoc />
    public ValueTask<AsyncSharedReferenceLease<OpenIdTeam>> TryGetAsync(
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var key = GetCacheKey(teamId);

        if (
            MemoryCache.TryGetValue<AsyncSharedReferenceLease<OpenIdTeam>>(
                key,
                out var existingLease
            ) && existingLease.TryAddReference(out var newLease)
        )
        {
            return ValueTask.FromResult(newLease);
        }

        AsyncSharedReferenceLease<OpenIdTeam> empty = default;
        return ValueTask.FromResult(empty);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(
        string teamId,
        AsyncSharedReferenceLease<OpenIdTeam> team,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var key = GetCacheKey(teamId);
        var newLease = team.AddReference();
        var entry = new object();

        // The team's own revision, then the revision of each team above it, because the team is materialized over them.
        var lineage = team.Value.GetRevisionLineage();

        // The generation token, the index write and the cache write share one lock, so two concurrent calls for the
        // same team leave the index holding the entry the cache holds, and a replaced entry's callback never removes
        // the survivor's index entry. The eviction callback does not take the lock.
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
                        State = (teamId, (entry, lineage)),
                    },
                },
            };

            foreach (var ancestor in lineage.Skip(1))
            {
                TeamIdsByAncestor.GetOrAdd(
                    ancestor.TeamId,
                    _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                )[teamId] = entry;
            }

            CachedRevisions[teamId] = (entry, lineage);
            MemoryCache.Set(key, newLease, entryOptions);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // The eviction callback releases the cache's reference; a lease already handed out keeps the team alive.
        MemoryCache.Remove(GetCacheKey(teamId));

        // A team is materialized over its parent, so every cached team beneath this one is evicted too.
        if (TeamIdsByAncestor.TryGetValue(teamId, out var descendantIds))
        {
            foreach (var descendantId in descendantIds.Keys)
            {
                MemoryCache.Remove(GetCacheKey(descendantId));
            }
        }

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

        // The eviction callbacks release the cache's references; a lease already handed out keeps its team alive.
        previous.Cancel();
        previous.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<TeamRevision>> GetRevisionsAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<TeamRevision> revisions =
        [
            .. CachedRevisions.Values.SelectMany(x => x.Lineage).Distinct(),
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
