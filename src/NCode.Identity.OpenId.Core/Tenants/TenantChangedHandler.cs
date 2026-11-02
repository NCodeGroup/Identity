// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Handles <see cref="TenantChangedCommand"/> by evicting the tenant from this replica's <see cref="IOpenIdTenantCache"/>
/// and, when the tenant's settings changed, seeding it again through the <see cref="ITenantProvisioner"/>. The seed fills
/// only what is missing, so a settings change that permits a new algorithm gets its key at once, and the key reaches the
/// other replicas through their secrets poll. After the seed, whether or not it succeeded, the handler announces the
/// change on the <see cref="TenantChangeBroadcastHandler.ChannelName"/> channel of the <see cref="IReplicaBroadcaster"/>
/// so every other replica evicts the tenant too. The eviction and the announcement run on their own bounded token, not
/// the request's, and a failed or timed-out announcement is logged and never fails the committed change: a replica that
/// missed it picks the change up through its polls.
/// </summary>
// See ADR-1169 (every tenant is provisioned with persisted keys), ADR-1205 (replicas announce changes through a
// broadcast port) and ADR-1351 (a committed change is applied and announced on its own bounded token).
internal sealed class TenantChangedHandler(
    IOpenIdTenantCache tenantCache,
    ISecretProtectorProvider secretProtectorProvider,
    IReplicaBroadcaster replicaBroadcaster,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<TenantChangedHandler> logger
) : ICommandHandler<TenantChangedCommand>
{
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<TenantChangedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        TenantChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        // The change is committed, so the eviction and the announcement run on their own bounded token, and the
        // announcement follows whatever the eviction and the seed did: the other replicas must still evict the tenant.
        // See ADR-1351.
        try
        {
            await PostCommitWork.RunAsync(TimeProvider, EvictAsync, command.TenantId);

            // Only a settings change can make the tenant need a key it lacks. Seeding after any other change would, say,
            // provision again at once the key a secret delete just removed.
            if (!command.SettingsChanged)
            {
                return;
            }

            // The provisioner is scoped (its mediator and unit of work are), and the change may be announced outside a
            // request, so the seed runs in its own scope. The seed keeps the request's token: a tenant that lacks a key
            // is seeded when it is next materialized.
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var provisioner = scope.ServiceProvider.GetRequiredService<ITenantProvisioner>();
            await provisioner.SeedAsync(command.TenantId, cancellationToken);
        }
        finally
        {
            await PostCommitWork.RunAsync(TimeProvider, BroadcastAsync, command.TenantId);
        }
    }

    /// <summary>
    /// Evicts the tenant from this replica's cache, and its master keys from this replica's memory.
    /// </summary>
    private async ValueTask EvictAsync(string tenantId, CancellationToken cancellationToken)
    {
        // Evict first: a request that misses the cache from here on materializes the tenant, which seeds it too, so the
        // eviction never serves a state older than the change.
        var propertyBag = PropertyBagFactory.Create();
        await TenantCache.RemoveAsync(tenantId, propertyBag, cancellationToken);

        // A deleted tenant's master keys must not stay usable on any replica, so they leave this replica's memory with
        // the tenant.
        var owner = SecretOwner.ForTenant(tenantId);
        SecretProtectorProvider.Evict(owner);
    }

    /// <summary>
    /// Announces the changed tenant to every replica, this one included. A failure is logged and swallowed, because the
    /// change is already committed.
    /// </summary>
    private async ValueTask BroadcastAsync(string tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await ReplicaBroadcaster.PublishAsync(
                TenantChangeBroadcastHandler.ChannelName,
                tenantId,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            // No caller token reaches the announcement, so every failure, its timeout included, is logged.
            // The change is already committed and this replica already evicted the tenant, so a failed announcement
            // only delays the other replicas until their polls.
            Logger.TenantChangeBroadcastFailed(tenantId, exception);
        }
    }
}
