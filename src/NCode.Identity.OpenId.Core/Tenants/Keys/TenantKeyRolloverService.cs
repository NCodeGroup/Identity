// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Events;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Rolls the keys of every seeded tenant over in the background, once the replica's server is materialized and then
/// every <see cref="OpenIdTenantKeyRolloverOptions.Interval"/>: it generates the successor of a key that nears its
/// retirement, activates a published key when no key signs, retires a key the tenant's settings do not use, and
/// removes an expired key the server generated. One replica runs each pass, under a lease on the shared-state port
/// that lasts half the interval, and each tenant is changed in its own unit of work under the tenant's optimistic
/// concurrency, so a pass that overlaps a seed or another pass loses the race for that tenant and leaves it to the
/// winner; the audit event of each change is published once the unit of work has committed, so a lost race records
/// nothing. A tenant that holds no key is left to its first materialization, which seeds it in full. A tenant the
/// pass changed is announced, so every replica evicts it and serves its keys as they are. The publishing and the
/// announcement follow a committed change, so each runs on its own bounded token, never the host's stopping token,
/// and the announcement follows whatever the publishing did.
/// </summary>
// See ADR-1351 (an effect that follows a committed change runs on its own bounded token) and ADR-1404.
internal sealed class TenantKeyRolloverService(
    IMaterializedServerAccessor materializedServerAccessor,
    IOptions<OpenIdOptions> optionsAccessor,
    ITenantKeyPolicy tenantKeyPolicy,
    ITenantKeyRolloverPlanner planner,
    ITenantKeyRollover rollover,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IStoreManagerFactory storeManagerFactory,
    IExpiringKeySet expiringKeySet,
    IEventPublisher eventPublisher,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<TenantKeyRolloverService> logger
) : BackgroundService
{
    /// <summary>
    /// The purpose that names the lease one replica takes on each pass.
    /// </summary>
    internal const string LeasePurpose = "tenant-key-rollover";

    // The tenants are read in pages, so the pass never holds every tenant's settings at once.
    private const int TenantPageSize = 100;

    private IMaterializedServerAccessor MaterializedServerAccessor { get; } =
        materializedServerAccessor;
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private ITenantKeyPolicy TenantKeyPolicy { get; } = tenantKeyPolicy;
    private ITenantKeyRolloverPlanner Planner { get; } = planner;
    private ITenantKeyRollover Rollover { get; } = rollover;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;
    private IEventPublisher EventPublisher { get; } = eventPublisher;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<TenantKeyRolloverService> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private OpenIdTenantOptions Options => OptionsAccessor.Value.Tenant;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The pass reads the store, so it waits for the replica to materialize its server, as every hosted
            // service that reads the store does; a replica that was down rolls its tenants over as soon as it is up.
            var server = await MaterializedServerAccessor.GetServerAsync(stoppingToken);
            await RollOverAsync(server.ServerId, stoppingToken);

            using var timer = new PeriodicTimer(Options.KeyRollover.Interval, TimeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RollOverAsync(server.ServerId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>
    /// Runs one pass over every seeded tenant, when this replica wins the pass's lease. A pass that fails is logged
    /// and run again at the next interval; a tenant that cannot be handled is logged and skipped, and the pass
    /// continues.
    /// </summary>
    /// <param name="serverId">The identifier of the server whose tenants are rolled over.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask RollOverAsync(string serverId, CancellationToken cancellationToken)
    {
        try
        {
            await RollOverCoreAsync(serverId, cancellationToken);
        }
        // A timeout throws OperationCanceledException with the token untouched; only a cancelled token is shutdown.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.TenantKeyRolloverPassFailed(exception);
        }
    }

    private async ValueTask RollOverCoreAsync(string serverId, CancellationToken cancellationToken)
    {
        var options = Options;
        if (!options.KeyProvisioning.Enabled)
            return;

        // The startup validator refuses options that cannot be honored, and the seed reports them.
        var resolution = TenantKeyPolicy.ResolveSpecifications(options.KeyProvisioning);
        if (resolution.Errors.Count > 0)
            return;

        // One replica walks the tenants in each interval; the others find the lease taken. The lease lasts half the
        // interval: one that lasted the whole interval would still be held, by the moments the pass took to start,
        // when the same replica's timer ticks next, and every other pass would be skipped. The per-tenant unit of
        // work, not the lease, is what keeps two writers from both changing a tenant's keys.
        var leaseKey = $"{LeasePurpose}:{serverId}";
        var leaseExpiresWhen = TimeProvider.GetUtcNow() + options.KeyRollover.Interval / 2;
        if (!await ExpiringKeySet.TryAddAsync(leaseKey, leaseExpiresWhen, cancellationToken))
            return;

        var serverSettings = await EffectiveSettingsResolver.ResolveServerAsync(cancellationToken);

        // The mediator is scoped (its handlers' unit of work is), and the pass runs outside any request, so the
        // announcements run in their own scope.
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var page = new PageRequest { Limit = TenantPageSize };
        do
        {
            PagedResult<PersistedTenantSettings> result;
            await using (
                var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken)
            )
            {
                var tenantStore = storeManager.GetStore<ITenantStore>();
                result = await tenantStore.GetSettingsPageAsync(page, cancellationToken);
            }

            foreach (var tenantSettings in result.Items)
            {
                try
                {
                    var committed = await RollOverTenantAsync(
                        tenantSettings,
                        serverSettings,
                        resolution.Specifications,
                        cancellationToken
                    );
                    if (committed is null)
                        continue;

                    await RunAfterCommitAsync(mediator, tenantSettings.TenantId, committed);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // One tenant that cannot be handled, say one with a settings document the server's settings
                    // reject, must not stop the pass: the others still need their keys rolled over.
                    Logger.TenantKeyRolloverFailed(tenantSettings.TenantId, exception);
                }
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }

    // The change is committed, so its audit events are published and the tenant is announced on their own bounded
    // tokens: the host's stopping token reaches neither, so a host that stops between the commit and the publish
    // cannot leave a committed change unrecorded, and the announcement follows whatever the publishing did, because
    // every replica must still evict the tenant and serve its keys as they are. See ADR-1351.
    private async ValueTask RunAfterCommitAsync(
        IMediator mediator,
        string tenantId,
        TenantKeyRolloverResult committed
    )
    {
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                PublishAuditEventsAsync,
                (tenantId, committed.AuditEvents)
            );
        }
        finally
        {
            // The settings did not change, so nothing seeds the tenant again in the announcement.
            await PostCommitWork.RunAsync(
                TimeProvider,
                static (state, token) =>
                    state.Mediator.SendAsync(
                        new TenantChangedCommand(state.TenantId, SettingsChanged: false),
                        token
                    ),
                (Mediator: mediator, TenantId: tenantId)
            );
        }
    }

    // A change is audited once it is committed; a change a lost race discarded never is. No caller token reaches the
    // publishing, so every failure, its timeout included, is logged: the change stands, and the audit trail is the
    // one record that lacks it.
    private async ValueTask PublishAuditEventsAsync(
        (string TenantId, IReadOnlyList<TenantKeyAuditEvent> AuditEvents) state,
        CancellationToken cancellationToken
    )
    {
        try
        {
            foreach (var auditEvent in state.AuditEvents)
            {
                await EventPublisher.PublishAsync(auditEvent, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Logger.TenantKeyAuditPublishFailed(state.TenantId, exception);
        }
    }

    // Changes one tenant's keys in its own unit of work and returns the committed result, or null when nothing was
    // committed.
    private async ValueTask<TenantKeyRolloverResult?> RollOverTenantAsync(
        PersistedTenantSettings tenantSettings,
        IReadOnlySettingCollection serverSettings,
        IReadOnlyList<TenantKeySpecification> specifications,
        CancellationToken cancellationToken
    )
    {
        var tenantId = tenantSettings.TenantId;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();

        // A tenant that holds no key has never been seeded, or is being seeded right now by its first
        // materialization, which gives it every key it uses; the pass only rolls over the keys of a seeded tenant.
        var secrets = await tenantStore.GetSecretsOrDefaultAsync(tenantId, cancellationToken);
        if (secrets is null || secrets.Value.Count == 0)
            return null;

        var settings = EffectiveSettingsResolver.Resolve(
            serverSettings,
            OpenIdSettingScopes.Tenant,
            tenantSettings.Value
        );

        var plan = Planner.Plan(specifications, secrets.Value, settings, TimeProvider.GetUtcNow());
        if (plan.IsEmpty)
            return null;

        var result = await Rollover.ApplyAsync(
            tenantStore,
            tenantId,
            secrets.Value,
            plan,
            cancellationToken
        );

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // A seed or another pass changed the tenant's keys first, from the same facts; its change stands, and the
            // next pass plans again from what it wrote.
            Logger.TenantKeyRolloverLostRace(tenantId);
            return null;
        }

        return result;
    }
}
