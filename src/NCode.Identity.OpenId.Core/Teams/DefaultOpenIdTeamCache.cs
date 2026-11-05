// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
/// </summary>
// See ADR-1411
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

    // Team identifiers compare without regard to case, so the key is the lowercase identifier.
    private static string GetCacheKey(string teamId) =>
        $"NCode.Identity.OpenId.Teams.DefaultOpenIdTeamCache:{teamId.ToLowerInvariant()}";

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
        string teamId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // The eviction callback releases the cache's reference; a lease already handed out keeps the team alive.
        MemoryCache.Remove(GetCacheKey(teamId));

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
