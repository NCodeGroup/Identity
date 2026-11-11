// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Destroys, for the provider that a <see cref="RemoteKeySweepJob"/> names, every key that the provider lists for this
/// deployment, that is older than the <see cref="RemoteKeyOptions.SweepGracePeriod"/> and that no stored secret names.
/// The provider's keys are listed before the stored references are read, so a key recorded during the pass is never
/// swept, and the grace period keeps a key whose creation is in flight. A key is destroyed through the
/// <see cref="IRemoteSecretDestroyer"/> and audited in the control-plane tenant once it is destroyed. A failure to list
/// the keys or to read the references propagates, so the dispatcher retries the job, and a repeated run is harmless.
/// </summary>
// See ADR-1478.
internal sealed class RemoteKeySweepJobHandler(
    IRemoteKeyProviderCatalog remoteKeyProviderCatalog,
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog,
    IStoreManagerFactory storeManagerFactory,
    IRemoteSecretDestroyer remoteSecretDestroyer,
    IEventPublisher eventPublisher,
    ITenantResolver tenantResolver,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<RemoteKeySweepJobHandler> logger
) : IDispatchJobHandler<RemoteKeySweepJob>
{
    private const int PageSize = 100;

    private IRemoteKeyProviderCatalog RemoteKeyProviderCatalog { get; } = remoteKeyProviderCatalog;
    private IRemoteKeyDescriptorCatalog RemoteKeyDescriptorCatalog { get; } =
        remoteKeyDescriptorCatalog;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IRemoteSecretDestroyer RemoteSecretDestroyer { get; } = remoteSecretDestroyer;
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private ITenantResolver TenantResolver { get; } = tenantResolver;
    private IOptions<SecretProtectionOptions> Options { get; } = options;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<RemoteKeySweepJobHandler> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private RemoteKeyOptions RemoteKeys => Options.Value.RemoteKeys;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        RemoteKeySweepJob job,
        DispatchContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(job);

        var deployment = RemoteKeys.Deployment;
        if (
            string.IsNullOrEmpty(deployment)
            || !RemoteKeyProviderCatalog.TryGetProvider(job.Provider, out var provider)
            || !RemoteKeyDescriptorCatalog.CreatesKeys(job.Provider)
        )
        {
            Logger.RemoteKeySweepSkipped(job.Provider);
            return;
        }

        // The catalog finds a provider by its ordinal name, so the job's name is the provider's name.
        var sweptBefore = TimeProvider.GetUtcNow() - RemoteKeys.SweepGracePeriod;

        // The keys are listed first, so a key that a secret records during the pass is never a candidate.
        var candidates = new List<RemoteKeyListing>();
        await foreach (
            var listing in provider.ListKeysAsync(
                new RemoteKeyListRequest { Deployment = deployment },
                cancellationToken
            )
        )
        {
            if (listing.CreatedWhen <= sweptBefore)
                candidates.Add(listing);
        }

        if (candidates.Count == 0)
            return;

        var referenced = await ReadReferencedKeyIdsAsync(job.Provider, cancellationToken);

        foreach (var listing in candidates)
        {
            if (referenced.Contains(listing.KeyId))
                continue;

            var destroyed = await RemoteSecretDestroyer.DestroyKeyAsync(
                new RemoteKeyReference(job.Provider, listing.KeyId)
            );
            if (!destroyed)
                continue;

            await PostCommitWork.RunAsync(
                TimeProvider,
                PublishAsync,
                CreateEvent(job.Provider, listing)
            );
        }
    }

    // Reads the key identifiers that every stored secret of every owner references through the provider, in keyset
    // pages that each open their own store manager.
    private async ValueTask<HashSet<string>> ReadReferencedKeyIdsAsync(
        string provider,
        CancellationToken cancellationToken
    )
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        var page = new PageRequest { Limit = PageSize };
        do
        {
            PagedResult<string> result;
            await using (
                var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken)
            )
            {
                result = await storeManager
                    .GetStore<IRemoteKeyReferenceStore>()
                    .GetKeyIdPageAsync(provider, page, cancellationToken);
            }

            referenced.UnionWith(result.Items);

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return referenced;
    }

    private RemoteKeyAuditEvent CreateEvent(string provider, RemoteKeyListing listing)
    {
        var now = TimeProvider.GetUtcNow();

        return new RemoteKeyAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = AuditEventSources.OpenId,
            TenantId = TenantResolver.GetControlPlaneTenantId(),
            Actor = AuditActor.System,
            Outcome = AuditOutcome.Success,
            Change = RemoteKeyChanges.Destroyed,
            Reason = RemoteKeyChangeReasons.Orphaned,
            Provider = provider,
            KeyId = listing.KeyId,
            GroupName = listing.GroupName,
            OwnerId = listing.OwnerId,
            SecretId = listing.SecretId,
            KeyCreatedWhen = listing.CreatedWhen,
        };
    }

    // A destruction is audited once it is done. No caller token reaches the publishing, so every failure, its timeout
    // included, is logged: the destruction stands, and the audit trail is the one record that lacks it.
    private async ValueTask PublishAsync(
        RemoteKeyAuditEvent auditEvent,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await EventPublisher.PublishAsync(auditEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.RemoteKeyAuditPublishFailed(auditEvent.Provider, auditEvent.KeyId, exception);
        }
    }
}
