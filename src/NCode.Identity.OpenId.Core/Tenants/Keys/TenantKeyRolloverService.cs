// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Rolls the keys of every seeded tenant over in the background, once the replica's server is materialized and then
/// every <see cref="OpenIdTenantKeyRolloverOptions.Interval"/>: it generates the successor of a key that nears its
/// retirement, activates a published key when no key signs, retires a key the tenant's settings no longer use, and
/// removes an expired key the server generated. One replica runs each pass, under a lease on the shared-state port
/// that lasts the interval, and each tenant is changed in its own unit of work under the tenant's optimistic
/// concurrency, so a pass that overlaps a seed or another pass loses the race for that tenant and leaves it to the
/// winner. A tenant that holds no key is left to its first materialization, which seeds it in full. A tenant the
/// pass changed is announced, so every replica evicts it and serves its keys as they are.
/// </summary>
// See ADR-1404.
internal sealed class TenantKeyRolloverService(
    IMaterializedServerAccessor materializedServerAccessor,
    IOptions<OpenIdOptions> optionsAccessor,
    ITenantKeyPolicy tenantKeyPolicy,
    ITenantKeyRolloverPlanner planner,
    ITenantKeyRollover rollover,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IStoreManagerFactory storeManagerFactory,
    IExpiringKeySet expiringKeySet,
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

        // One replica walks the tenants in each interval; the others find the lease taken. The per-tenant unit of
        // work, not the lease, is what keeps two writers from both changing a tenant's keys.
        var leaseKey = $"{LeasePurpose}:{serverId}";
        var leaseExpiresWhen = TimeProvider.GetUtcNow() + options.KeyRollover.Interval;
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
                    var changed = await RollOverTenantAsync(
                        tenantSettings,
                        serverSettings,
                        resolution.Specifications,
                        cancellationToken
                    );
                    if (!changed)
                        continue;

                    // The change is committed, so every replica evicts the tenant and serves its keys as they are;
                    // the settings did not change, so nothing seeds the tenant again in the announcement.
                    await mediator.SendAsync(
                        new TenantChangedCommand(tenantSettings.TenantId, SettingsChanged: false),
                        cancellationToken
                    );
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

    // Changes one tenant's keys in its own unit of work and returns whether anything was committed.
    private async ValueTask<bool> RollOverTenantAsync(
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
            return false;

        var settings = EffectiveSettingsResolver.Resolve(
            serverSettings,
            OpenIdSettingScopes.Tenant,
            tenantSettings.Value
        );

        var plan = Planner.Plan(specifications, secrets.Value, settings, TimeProvider.GetUtcNow());
        if (plan.IsEmpty)
            return false;

        await Rollover.ApplyAsync(tenantStore, tenantId, secrets.Value, plan, cancellationToken);

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // A seed or another pass changed the tenant's keys first, from the same facts; its change stands, and the
            // next pass plans again from what it wrote.
            Logger.TenantKeyRolloverLostRace(tenantId);
            return false;
        }

        return true;
    }
}
