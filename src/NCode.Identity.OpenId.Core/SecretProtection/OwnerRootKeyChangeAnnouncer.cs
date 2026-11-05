// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Mediator;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Tells every replica that the root key of an owner changed, so that each one drops the master keys it holds for the
/// owner and reads them again under the key's new state. For a tenant the announcement is the tenant-changed command,
/// which evicts the tenant's materialized keys and master keys here and broadcasts to every other replica; for any other
/// owner it is the master key broadcast.
/// </summary>
/// <remarks>
/// The command is sent from a scope of its own, because the change may be announced outside a request. The caller
/// supplies a bounded token, as the observer contract says.
/// </remarks>
// See ADR-1411.
internal sealed class OwnerRootKeyChangeAnnouncer(
    IServiceScopeFactory serviceScopeFactory,
    IReplicaBroadcaster replicaBroadcaster
) : IOwnerRootKeyChangeObserver
{
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;

    private IReplicaBroadcaster ReplicaBroadcaster { get; } = replicaBroadcaster;

    /// <inheritdoc />
    public async ValueTask ChangedAsync(SecretOwner owner, CancellationToken cancellationToken)
    {
        if (
            !string.Equals(
                owner.OwnerType,
                SecretOwnerTypes.Tenant,
                StringComparison.OrdinalIgnoreCase
            )
        )
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

        // The settings did not change, so nothing seeds the tenant again in the announcement.
        await mediator.SendAsync(
            new TenantChangedCommand(owner.OwnerId, SettingsChanged: false),
            cancellationToken
        );
    }
}
