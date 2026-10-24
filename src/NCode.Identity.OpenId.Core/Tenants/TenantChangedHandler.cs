#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
/// so every other replica evicts the tenant too. A failed announcement is logged and never fails the committed change:
/// a replica that missed it picks the change up through its polls.
/// </summary>
// See ADR-0169 (every tenant is provisioned with persisted keys) and ADR-0205 (replicas announce changes through a
// broadcast port).
internal sealed class TenantChangedHandler(
    IOpenIdTenantCache tenantCache,
    ISecretProtectorProvider secretProtectorProvider,
    IReplicaBroadcaster replicaBroadcaster,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TenantChangedHandler> logger
) : ICommandHandler<TenantChangedCommand>
{
    private IOpenIdTenantCache TenantCache { get; } = tenantCache;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private ILogger<TenantChangedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        TenantChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        // Evict first: a request that misses the cache from here on materializes the tenant, which seeds it too, so the
        // eviction never serves a state older than the change.
        await TenantCache.RemoveAsync(
            command.TenantId,
            PropertyBagFactory.Create(),
            cancellationToken
        );

        // A deleted tenant's master keys must not stay usable on any replica, so they leave this replica's memory with
        // the tenant.
        SecretProtectorProvider.Evict(SecretOwner.ForTenant(command.TenantId));

        // The announcement follows the seed even when the seed fails: the change is committed either way, so the other
        // replicas must still evict the tenant.
        try
        {
            // Only a settings change can make the tenant need a key it lacks. Seeding after any other change would, say,
            // provision again at once the key a secret delete just removed.
            if (!command.SettingsChanged)
            {
                return;
            }

            // The provisioner is scoped (its mediator and unit of work are), and the change may be announced outside a
            // request, so the seed runs in its own scope.
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var provisioner = scope.ServiceProvider.GetRequiredService<ITenantProvisioner>();
            await provisioner.SeedAsync(command.TenantId, cancellationToken);
        }
        finally
        {
            await BroadcastAsync(command.TenantId, cancellationToken);
        }
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change is already committed and this replica already evicted the tenant, so a failed announcement
            // only delays the other replicas until their polls.
            Logger.TenantChangeBroadcastFailed(tenantId, exception);
        }
    }
}
