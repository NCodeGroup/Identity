// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Mediator;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Tells every replica that the root key of an owner changed, so that each one drops the master keys it holds for the
/// owner and reads them again under the key's new state. For a tenant the announcement is the tenant-changed command,
/// which evicts the tenant's materialized keys and master keys here and broadcasts to every other replica; for any other
/// owner it is the master key broadcast. For a tenant the change first marks the tenant's secrets changed.
/// </summary>
/// <remarks>
/// The command is sent from a scope of its own, because the change may be announced outside a request. The caller
/// supplies a bounded token, as the observer contract says.
/// </remarks>
// See ADR-1414 and ADR-1416.
internal sealed class OwnerRootKeyChangeAnnouncer(
    IServiceScopeFactory serviceScopeFactory,
    IReplicaBroadcaster replicaBroadcaster,
    IStoreManagerFactory storeManagerFactory
) : IOwnerRootKeyChangeObserver
{
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;

    /// <inheritdoc />
    public async ValueTask ChangedAsync(SecretOwner owner, CancellationToken cancellationToken)
    {
        if (owner.TenantId is null)
        {
            await ReplicaBroadcaster.PublishAsync(
                MasterKeyBroadcast.ChannelName,
                owner.ToString(),
                cancellationToken
            );
            return;
        }

        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // The row change lets a replica that missed the announcement find the tenant stale. The command is sent even
        // when the mark fails, whose exception the manager logs.
        try
        {
            await MarkSecretsChangedAsync(owner.OwnerId, cancellationToken);
        }
        finally
        {
            // The settings did not change, so nothing seeds the tenant again in the announcement.
            await mediator.SendAsync(
                new TenantChangedCommand(owner.OwnerId, SettingsChanged: false),
                cancellationToken
            );
        }
    }

    private async ValueTask MarkSecretsChangedAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var changed = await tenantStore.MarkSecretsChangedAsync(tenantId, cancellationToken);
        if (changed)
            await storeManager.SaveChangesAsync(cancellationToken);
    }
}
