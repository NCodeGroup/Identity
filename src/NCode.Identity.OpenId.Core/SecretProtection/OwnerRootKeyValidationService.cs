// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Validates the root keys that owners bring in the background, once the replica's server is materialized and then every
/// <see cref="OwnerRootKeyOptions.RetryInterval"/>: it probes the keys that come due, which are an active key every
/// <see cref="OwnerRootKeyOptions.ValidationInterval"/> and an unavailable or removing key every retry interval, and
/// completes a rotation or a removal that an earlier call left unfinished. One replica runs each pass, under a lease on
/// the shared-state port that lasts half the retry interval, and each owner is handled in its own try block, so one
/// owner that fails is logged and the others still run. The audit events of a change are published once it has
/// committed, on their own bounded token.
/// </summary>
/// <remarks>
/// A key that fails its probe makes the owner fail closed on every replica: the manager tells the observers, which evict
/// the owner everywhere. The ring of each replica also unwraps the owner's master keys again every validation interval,
/// so a replica that missed the announcement still stops.
/// </remarks>
// See ADR-1351 (an effect that follows a committed change runs on its own bounded token) and ADR-1411.
internal sealed class OwnerRootKeyValidationService(
    IMaterializedServerAccessor materializedServerAccessor,
    IOwnerRootKeyManager ownerRootKeyManager,
    IStoreManagerFactory storeManagerFactory,
    IExpiringKeySet expiringKeySet,
    IEventPublisher eventPublisher,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<OwnerRootKeyValidationService> logger
) : BackgroundService
{
    /// <summary>
    /// The purpose that names the lease one replica takes on each pass.
    /// </summary>
    internal const string LeasePurpose = "owner-root-key-validation";

    private const int PageSize = 100;

    private IMaterializedServerAccessor MaterializedServerAccessor { get; } =
        materializedServerAccessor;
    private IOwnerRootKeyManager OwnerRootKeyManager { get; } = ownerRootKeyManager;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private IOptions<SecretProtectionOptions> Options { get; } = options;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<OwnerRootKeyValidationService> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private OwnerRootKeyOptions RootKeys => Options.Value.OwnerRootKeys;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The pass reads the store, so it waits for the replica to materialize its server, as every hosted
            // service that reads the store does.
            var server = await MaterializedServerAccessor.GetServerAsync(stoppingToken);
            await RunAsync(server.ServerId, stoppingToken);

            using var timer = new PeriodicTimer(RootKeys.RetryInterval, TimeProvider);
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
    /// <param name="serverId">The identifier of the server whose owners are validated.</param>
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
            Logger.OwnerRootKeyValidationPassFailed(exception);
        }
    }

    private async ValueTask RunCoreAsync(string serverId, CancellationToken cancellationToken)
    {
        // The lease lasts half the interval: one that lasted the whole interval would still be held when the same
        // replica's timer ticks next.
        var leaseKey = $"{LeasePurpose}:{serverId}";
        var now = TimeProvider.GetUtcNow();
        if (
            !await ExpiringKeySet.TryAddAsync(
                leaseKey,
                now + RootKeys.RetryInterval / 2,
                cancellationToken
            )
        )
        {
            return;
        }

        var validatedBefore = now - RootKeys.ValidationInterval;
        var retryBefore = now - RootKeys.RetryInterval;

        var page = new PageRequest { Limit = PageSize };
        do
        {
            PagedResult<PersistedOwnerRootKey> result;
            await using (
                var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken)
            )
            {
                result = await storeManager
                    .GetStore<IOwnerRootKeyStore>()
                    .GetDuePageAsync(validatedBefore, retryBefore, page, cancellationToken);
            }

            foreach (var row in result.Items)
            {
                await ValidateAsync(row.Owner, cancellationToken);
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }

    private async ValueTask ValidateAsync(SecretOwner owner, CancellationToken cancellationToken)
    {
        try
        {
            var validation = await OwnerRootKeyManager.ValidateAsync(owner, cancellationToken);

            await PostCommitWork.RunAsync(
                TimeProvider,
                PublishAsync,
                (owner, Events: CreateEvents(validation))
            );
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // One owner that cannot be validated must not stop the pass.
            Logger.OwnerRootKeyValidationOwnerFailed(owner, exception);
        }
    }

    private List<AuditEvent> CreateEvents(OwnerRootKeyValidation validation)
    {
        List<AuditEvent> events = [];

        if (validation is { RootKey: { } rootKey, Change: { } change })
        {
            var failed = string.Equals(
                change,
                RootKeyChanges.Unavailable,
                StringComparison.Ordinal
            );
            events.Add(
                new RootKeyAuditEvent
                {
                    EventId = Guid.CreateVersion7(TimeProvider.GetUtcNow()),
                    EventTime = TimeProvider.GetUtcNow(),
                    EventSource = AuditEventSources.OpenId,
                    TenantId = GetTenantId(rootKey.Owner),
                    Actor = AuditActor.System,
                    Outcome = failed ? AuditOutcome.Failure : AuditOutcome.Success,
                    Change = change,
                    Reason = failed
                        ? RootKeyChangeReasons.ValidationFailed
                        : RootKeyChangeReasons.ValidationSucceeded,
                    OwnerType = rootKey.Owner.OwnerType,
                    OwnerId = rootKey.Owner.OwnerId,
                    KeyWrapperName = rootKey.KeyWrapperName,
                    KeyReference = rootKey.KeyReference,
                    State = rootKey.State,
                    Failure = failed ? rootKey.Failure : null,
                }
            );
        }

        if (validation.Rotation is { } rotation)
        {
            events.Add(
                new MasterKeyAuditEvent
                {
                    EventId = Guid.CreateVersion7(TimeProvider.GetUtcNow()),
                    EventTime = TimeProvider.GetUtcNow(),
                    EventSource = AuditEventSources.OpenId,
                    TenantId = GetTenantId(rotation.Owner),
                    Actor = AuditActor.System,
                    Outcome = AuditOutcome.Success,
                    Change = MasterKeyChanges.Rotated,
                    Reason = MasterKeyChangeReasons.RootKeyChanged,
                    OwnerType = rotation.Owner.OwnerType,
                    OwnerId = rotation.Owner.OwnerId,
                    KeyId = rotation.ActiveKey.KeyId,
                    Version = rotation.ActiveKey.Version,
                    RootKeyProvider = rotation.ActiveKey.RootKeyProvider,
                    RootKeyReference = rotation.ActiveKey.RootKeyReference,
                    RetiredKeyId = rotation.RetiredKey?.KeyId,
                    RetiredVersion = rotation.RetiredKey?.Version,
                }
            );
        }

        return events;
    }

    private static string? GetTenantId(SecretOwner owner) =>
        string.Equals(owner.OwnerType, SecretOwnerTypes.Tenant, StringComparison.OrdinalIgnoreCase)
            ? owner.OwnerId
            : null;

    // A change is audited once it is committed. No caller token reaches the publishing, so every failure, its timeout
    // included, is logged: the change stands, and the audit trail is the one record that lacks it.
    private async ValueTask PublishAsync(
        (SecretOwner Owner, List<AuditEvent> Events) state,
        CancellationToken cancellationToken
    )
    {
        try
        {
            foreach (var auditEvent in state.Events)
            {
                await EventPublisher.PublishAsync(auditEvent, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Logger.OwnerRootKeyAuditPublishFailed(state.Owner, exception);
        }
    }
}
