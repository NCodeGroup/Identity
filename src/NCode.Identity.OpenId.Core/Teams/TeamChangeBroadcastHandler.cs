// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Handles the announcement that a team changed by evicting the team from this replica's <see cref="IOpenIdTeamCache"/>
/// and its tenants from the <see cref="IOpenIdTenantCache"/>. Every replica receives the announcement, the one that made
/// the change included. The message is the team identifier. Evicting is idempotent, so a repeated message is harmless. A
/// replica that may have missed an announcement evicts only the teams whose stored row changed or is gone, with their
/// tenants, and clears both caches when it cannot read the revisions.
/// </summary>
// See ADR-1411 and ADR-1416.
internal sealed class TeamChangeBroadcastHandler(
    IOpenIdTeamCache teamCache,
    IOpenIdTenantCache tenantCache,
    IStoreManagerFactory storeManagerFactory,
    ILogger<TeamChangeBroadcastHandler> logger
) : IReplicaBroadcastHandler
{
    /// <summary>
    /// The maximum number of teams whose concurrency tokens are read in one store call.
    /// </summary>
    internal const int TeamBatchSize = 500;

    /// <summary>
    /// The name of the replica broadcast channel on which a changed team is announced.
    /// </summary>
    public const string ChannelName = "team-changed";

    private IOpenIdTeamCache TeamCache { get; } = teamCache;
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ILogger<TeamChangeBroadcastHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public string Channel => ChannelName;

    /// <inheritdoc />
    public async ValueTask HandleAsync(string message, CancellationToken cancellationToken)
    {
        var propertyBag = PropertyBagFactory.Create();

        // The team's tenants are evicted even when the team's removal fails, and the failure still propagates.
        try
        {
            await TeamCache.RemoveAsync(message, propertyBag, cancellationToken);
        }
        finally
        {
            await TenantCache.RemoveTeamAsync(message, propertyBag, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken)
    {
        var propertyBag = PropertyBagFactory.Create();

        try
        {
            List<TeamRevision> served =
            [
                .. await TeamCache.GetRevisionsAsync(propertyBag, cancellationToken),
                .. await TenantCache.GetTeamRevisionsAsync(propertyBag, cancellationToken),
            ];

            if (served.Count == 0)
                return;

            var teamIds = served
                .Select(revision => revision.TeamId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var currentTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            await using (
                var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken)
            )
            {
                var store = storeManager.GetStore<ITeamStore>();

                foreach (var batch in teamIds.Chunk(TeamBatchSize))
                {
                    var tokens = await store.GetConcurrencyTokensAsync(batch, cancellationToken);
                    foreach (var (teamId, token) in tokens)
                    {
                        currentTokens[teamId] = token;
                    }
                }
            }

            var staleTeamIds = served
                .Where(revision =>
                    !currentTokens.TryGetValue(revision.TeamId, out var token)
                    || !string.Equals(token, revision.ConcurrencyToken, StringComparison.Ordinal)
                )
                .Select(revision => revision.TeamId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var teamId in staleTeamIds)
            {
                await HandleAsync(teamId, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TeamCacheRevalidationFailed(exception);
            await ClearCachesAsync(propertyBag, cancellationToken);
        }
    }

    private async ValueTask ClearCachesAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // Each cache is cleared on its own, so a failed team-cache clear still clears the tenants.
        try
        {
            await TeamCache.ClearAsync(propertyBag, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TeamCacheClearFailed(exception);
        }

        try
        {
            await TenantCache.ClearAsync(propertyBag, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TenantCacheClearFailed(exception);
        }
    }
}
