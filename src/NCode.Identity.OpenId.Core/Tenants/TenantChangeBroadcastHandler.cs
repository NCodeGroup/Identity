// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Handles the announcement that a tenant changed by evicting the tenant from this replica's
/// <see cref="IOpenIdTenantCache"/>. Every replica receives the announcement, the one that made the change included, so
/// the next request on any replica materializes the tenant again. The message is the tenant identifier. Evicting is
/// idempotent, so the second eviction on the replica that made the change, and a repeated message, are harmless. A
/// replica that may have missed an announcement evicts only the tenants whose stored row changed or is gone, with their
/// master keys, and evicts every cached tenant and master key when it cannot read the revisions.
/// </summary>
// See ADR-1205 and ADR-1454.
internal sealed class TenantChangeBroadcastHandler(
    IOpenIdTenantCache tenantCache,
    ISecretProtectorProvider secretProtectorProvider,
    IStoreManagerFactory storeManagerFactory,
    ILogger<TenantChangeBroadcastHandler> logger
) : IReplicaBroadcastHandler
{
    /// <summary>
    /// The maximum number of tenants whose concurrency tokens are read in one store call.
    /// </summary>
    internal const int TenantBatchSize = 500;

    /// <summary>
    /// The name of the replica broadcast channel on which a changed tenant is announced.
    /// </summary>
    public const string ChannelName = "tenant-changed";

    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ILogger<TenantChangeBroadcastHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public string Channel => ChannelName;

    /// <inheritdoc />
    public ValueTask HandleAsync(string message, CancellationToken cancellationToken)
    {
        // A deleted tenant's master keys must not stay usable on any replica, so they leave this replica's memory with
        // the tenant.
        SecretProtectorProvider.Evict(SecretOwner.ForTenant(message));

        return TenantCache.RemoveAsync(message, PropertyBagFactory.Create(), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask HandleMissedMessagesAsync(CancellationToken cancellationToken)
    {
        var propertyBag = PropertyBagFactory.Create();

        var staleTenantIds = await GetStaleTenantIdsAsync(propertyBag, cancellationToken);
        if (staleTenantIds is null)
        {
            await EvictEverythingAsync(propertyBag, cancellationToken);
            return;
        }

        await EvictTenantsAsync(staleTenantIds, propertyBag, cancellationToken);
    }

    private async ValueTask<IReadOnlyList<string>?> GetStaleTenantIdsAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var served = await TenantCache.GetRevisionsAsync(propertyBag, cancellationToken);
            if (served.Count == 0)
                return [];

            var tenantIds = served.Select(revision => revision.TenantId).Distinct().ToList();

            var currentTokens = await ReadCurrentTokensAsync(tenantIds, cancellationToken);

            return served
                .Where(revision =>
                    !currentTokens.TryGetValue(revision.TenantId, out var token)
                    || !string.Equals(token, revision.ConcurrencyToken, StringComparison.Ordinal)
                )
                .Select(revision => revision.TenantId)
                .Distinct()
                .ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.TenantCacheRevalidationFailed(exception);
            return null;
        }
    }

    private async ValueTask<Dictionary<string, string>> ReadCurrentTokensAsync(
        List<string> tenantIds,
        CancellationToken cancellationToken
    )
    {
        var currentTokens = new Dictionary<string, string>(
            tenantIds.Count,
            StringComparer.OrdinalIgnoreCase
        );

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        foreach (var batch in tenantIds.Chunk(TenantBatchSize))
        {
            var tokens = await store.GetConcurrencyTokensAsync(batch, cancellationToken);
            foreach (var (tenantId, token) in tokens)
            {
                currentTokens[tenantId] = token;
            }
        }

        return currentTokens;
    }

    private async ValueTask EvictTenantsAsync(
        IReadOnlyList<string> tenantIds,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // One failing tenant never stops the others; a failure evicts every cached tenant and master key so no stale
        // entry survives.
        var failed = false;

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await HandleAsync(tenantId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Logger.TenantCacheEvictionFailed(exception, tenantId);
                failed = true;
            }
        }

        if (failed)
            await EvictEverythingAsync(propertyBag, cancellationToken);
    }

    private async ValueTask EvictEverythingAsync(
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // A missed message may have announced a deleted tenant, so every owner's master keys leave this replica's
        // memory.
        SecretProtectorProvider.EvictAll();

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
