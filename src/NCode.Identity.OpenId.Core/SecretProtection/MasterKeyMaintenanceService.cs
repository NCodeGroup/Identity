// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Keeps master keys fresh in the background, once the replica's server is materialized and then every
/// <see cref="MasterKeyOptions.MaintenanceInterval"/>: it rotates every active key older than the
/// <see cref="MasterKeyOptions.RotationPeriod"/>, and for every owner that has retired keys it rewraps the values they
/// protect and destroys the keys that nothing names after their grace. One replica runs each pass, under a lease on the
/// shared-state port that lasts half the interval, and each owner is handled in its own try block, so one owner that
/// fails is logged and the others still run. The audit event of a change is published once it has committed, on its own
/// bounded token.
/// </summary>
/// <remarks>
/// This is the durable path: the rewrap job a rotation dispatches only brings the same work forward, and the pass never
/// runs on the dispatcher.
/// </remarks>
// See ADR-1351 (an effect that follows a committed change runs on its own bounded token) and ADR-1407.
internal sealed class MasterKeyMaintenanceService(
    IMaterializedServerAccessor materializedServerAccessor,
    IMasterKeyManager masterKeyManager,
    IStoreManagerFactory storeManagerFactory,
    IExpiringKeySet expiringKeySet,
    IEventPublisher eventPublisher,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<MasterKeyMaintenanceService> logger
) : BackgroundService
{
    /// <summary>
    /// The purpose that names the lease one replica takes on each pass.
    /// </summary>
    internal const string LeasePurpose = "master-key-maintenance";

    private const int PageSize = 100;

    private IMaterializedServerAccessor MaterializedServerAccessor { get; } =
        materializedServerAccessor;
    private IMasterKeyManager MasterKeyManager { get; } = masterKeyManager;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private IOptions<SecretProtectionOptions> Options { get; } = options;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<MasterKeyMaintenanceService> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private MasterKeyOptions MasterKeys => Options.Value.MasterKeys;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The pass reads the store, so it waits for the replica to materialize its server, as every hosted
            // service that reads the store does.
            var server = await MaterializedServerAccessor.GetServerAsync(stoppingToken);
            await RunAsync(server.ServerId, stoppingToken);

            using var timer = new PeriodicTimer(MasterKeys.MaintenanceInterval, TimeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunAsync(server.ServerId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>
    /// Runs one pass, when this replica wins the pass's lease. A pass that fails is logged and run again at the next
    /// interval.
    /// </summary>
    /// <param name="serverId">The identifier of the server whose master keys are maintained.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask RunAsync(string serverId, CancellationToken cancellationToken)
    {
        try
        {
            await RunCoreAsync(serverId, cancellationToken);
        }
        // A timeout throws OperationCanceledException with the token untouched; only a cancelled token is shutdown.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.MasterKeyMaintenancePassFailed(exception);
        }
    }

    private async ValueTask RunCoreAsync(string serverId, CancellationToken cancellationToken)
    {
        // The lease lasts half the interval, as the tenant key rollover's does: one that lasted the whole interval
        // would still be held when the same replica's timer ticks next.
        var leaseKey = $"{LeasePurpose}:{serverId}";
        var leaseExpiresWhen = TimeProvider.GetUtcNow() + MasterKeys.MaintenanceInterval / 2;
        if (!await ExpiringKeySet.TryAddAsync(leaseKey, leaseExpiresWhen, cancellationToken))
            return;

        await RotateOverdueAsync(cancellationToken);
        await RewrapAndDestroyRetiredAsync(cancellationToken);
    }

    // Rotates the owners whose active key outlived the rotation period.
    private async ValueTask RotateOverdueAsync(CancellationToken cancellationToken)
    {
        var createdBefore = TimeProvider.GetUtcNow() - MasterKeys.RotationPeriod;

        var page = new PageRequest { Limit = PageSize };
        do
        {
            PagedResult<PersistedMasterKey> result;
            await using (
                var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken)
            )
            {
                result = await storeManager
                    .GetStore<IMasterKeyStore>()
                    .GetActivePageAsync(createdBefore, page, cancellationToken);
            }

            foreach (var row in result.Items)
            {
                await RotateAsync(row.Owner, cancellationToken);
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }

    private async ValueTask RotateAsync(SecretOwner owner, CancellationToken cancellationToken)
    {
        try
        {
            var rotation = await MasterKeyManager.RotateAsync(owner, cancellationToken);

            await PostCommitWork.RunAsync(
                TimeProvider,
                PublishAsync,
                (
                    owner,
                    CreateEvent(
                        MasterKeyChanges.Rotated,
                        MasterKeyChangeReasons.RotationPeriodElapsed,
                        rotation.ActiveKey,
                        rotation.RetiredKey
                    )
                )
            );
        }
        catch (StoreConcurrencyException)
        {
            // Another writer rotated the key first, and its rotation stands.
            Logger.MasterKeyRotationLostRace(owner);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.MasterKeyMaintenanceOwnerFailed(owner, exception);
        }
    }

    // Rewraps the values of every owner that has retired keys, then destroys the keys that nothing names. The owners are
    // read in keyset pages that carry no key material, and an owner with retired keys on several pages is handled once.
    private async ValueTask RewrapAndDestroyRetiredAsync(CancellationToken cancellationToken)
    {
        // The owners are normalized, so the default equality of the owner finds the ones already handled.
        var handled = new HashSet<SecretOwner>();

        var page = new PageRequest { Limit = PageSize };
        do
        {
            PagedResult<SecretOwner> result;
            await using (
                var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken)
            )
            {
                result = await storeManager
                    .GetStore<IMasterKeyStore>()
                    .GetRetiredOwnerPageAsync(page, cancellationToken);
            }

            foreach (var item in result.Items)
            {
                var owner = item.Normalize();
                if (handled.Add(owner))
                    await RewrapAndDestroyAsync(owner, cancellationToken);
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }

    private async ValueTask RewrapAndDestroyAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await MasterKeyManager.RewrapAsync(owner, cancellationToken);

            var destroyed = await MasterKeyManager.DestroyRetiredAsync(owner, cancellationToken);
            foreach (var key in destroyed)
            {
                await PostCommitWork.RunAsync(
                    TimeProvider,
                    PublishAsync,
                    (
                        owner,
                        CreateEvent(
                            MasterKeyChanges.Destroyed,
                            MasterKeyChangeReasons.Unreferenced,
                            key,
                            null
                        )
                    )
                );
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // One owner that cannot be handled must not stop the pass.
            Logger.MasterKeyMaintenanceOwnerFailed(owner, exception);
        }
    }

    private MasterKeyAuditEvent CreateEvent(
        string change,
        string reason,
        MasterKeyInfo key,
        MasterKeyInfo? retired
    )
    {
        var now = TimeProvider.GetUtcNow();
        var owner = key.Owner;

        return new MasterKeyAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = string.Equals(
                owner.OwnerType,
                SecretOwnerTypes.Tenant,
                StringComparison.OrdinalIgnoreCase
            )
                ? owner.OwnerId
                : null,
            Actor = AuditActor.System,
            Outcome = AuditOutcome.Success,
            Change = change,
            Reason = reason,
            OwnerType = owner.OwnerType,
            OwnerId = owner.OwnerId,
            KeyId = key.KeyId,
            Version = key.Version,
            RootKeyProvider = key.RootKeyProvider,
            RetiredKeyId = retired?.KeyId,
            RetiredVersion = retired?.Version,
        };
    }

    // A change is audited once it is committed. No caller token reaches the publishing, so every failure, its timeout
    // included, is logged: the change stands, and the audit trail is the one record that lacks it.
    private async ValueTask PublishAsync(
        (SecretOwner Owner, MasterKeyAuditEvent AuditEvent) state,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await EventPublisher.PublishAsync(state.AuditEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.MasterKeyAuditPublishFailed(state.Owner, exception);
        }
    }
}
