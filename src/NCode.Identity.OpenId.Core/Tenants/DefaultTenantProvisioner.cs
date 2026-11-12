// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Persistence;
using NCode.Identity.Persistence.Stores;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the default implementation of <see cref="ITenantProvisioner"/>. It creates a tenant at the address its
/// strategy derives for it, judged by the same rules as every other tenant written, and it seeds a tenant — after the root
/// tenant — by fanning out <see cref="SeedTenantCommand"/> to every registered handler within a single unit of work,
/// then runs the steps the handlers queued for after the commit, each on its own bounded token; a step that fails is
/// logged, the remaining steps run, and the seed stands, because its unit of work has committed. It then reads the tenant
/// again in a fresh unit of work and returns the row its seed committed.
/// </summary>
// See ADR-1166 (one seeding pipeline), ADR-1166 (a tenant is provisioned through one pipeline under every strategy),
// ADR-1351 (an effect that follows a committed change runs on its own bounded token) and ADR-1166 (a seeded tenant is
// returned as its seed committed it).
internal sealed class DefaultTenantProvisioner(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    ITenantResolver tenantResolver,
    IOptions<TenantResolutionOptions> optionsAccessor,
    IMediator mediator,
    ITeamProvisioner teamProvisioner,
    TimeProvider timeProvider,
    ILogger<DefaultTenantProvisioner> logger
) : ITenantProvisioner
{
    // One retry is enough to converge after losing a race to a concurrent seed of the same tenant (see SeedAsync).
    private const int MaxSeedAttempts = 2;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private ITenantResolver TenantResolver { get; } = tenantResolver;
    private IMediator Mediator { get; } = mediator;
    private ITeamProvisioner TeamProvisioner { get; } = teamProvisioner;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<DefaultTenantProvisioner> Logger { get; } = logger;

    private RootTenantOptions RootOptions => optionsAccessor.Value.Root;

    /// <inheritdoc />
    public async ValueTask<PersistedTenant> EnsureCreatedAsync(
        string teamId,
        string tenantId,
        string displayName,
        CancellationToken cancellationToken
    )
    {
        // The tenant's team exists before the tenant, so the tenant's foreign key to it holds (ADR-1411).
        await TeamProvisioner.EnsureCreatedAsync(
            teamId,
            string.Equals(teamId, RootOptions.TeamId, StringComparison.Ordinal)
                ? RootOptions.TeamDisplayName
                : teamId,
            cancellationToken
        );

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var existing = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (existing is not null)
            return existing;

        // A tenant created on demand is written like any other: at the address its strategy derives for it, judged by
        // the rules every tenant write applies (ADR-1160, ADR-1165).
        var issuer = await TenantResolver.GetDefaultIssuerAsync(tenantId, cancellationToken);
        var mtlsEndpointAliasAuthority =
            await TenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync(
                tenantId,
                cancellationToken
            );
        var failures = await TenantResolver.ValidateTenantAsync(
            tenantId,
            issuer,
            mtlsEndpointAliasAuthority,
            storeManager,
            cancellationToken
        );
        if (failures.Count > 0)
        {
            var details = string.Join(" ", failures.Select(failure => failure.Detail));
            throw new InvalidOperationException(
                $"The tenant '{tenantId}' cannot be created at its default address: {details}"
            );
        }

        var persistedTenant = CreateEmptyPersistedTenant(
            teamId,
            tenantId,
            displayName,
            issuer,
            mtlsEndpointAliasAuthority
        );
        await store.AddAsync(persistedTenant, cancellationToken);

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Another node or request may have created the tenant between the read above and this write — two replicas
            // reaching the root tenant on a fresh deployment, say. The row it wrote is the tenant; anything else is a
            // genuine failure.
            var concurrent = await FindTenantAsync(tenantId, cancellationToken);
            if (concurrent is null)
                throw;

            return concurrent;
        }

        return persistedTenant;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenant> SeedAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var rootTenantId = RootOptions.TenantId;
        var isRootTenant = string.Equals(tenantId, rootTenantId, StringComparison.Ordinal);

        // The root (control-plane) tenant must exist and be seeded whatever tenant is seeded, so its control-plane data is
        // in place under every strategy (ADR-1411, ADR-1166). It is seeded on this same path, so root and workload tenants
        // are seeded identically.
        if (!isRootTenant)
        {
            await EnsureCreatedAsync(
                RootOptions.TeamId,
                rootTenantId,
                RootOptions.DisplayName,
                cancellationToken
            );
            await SeedInUnitOfWorkAsync(rootTenantId, TenantPlane.Root, cancellationToken);
        }

        await SeedInUnitOfWorkAsync(
            tenantId,
            isRootTenant ? TenantPlane.Root : TenantPlane.Workload,
            cancellationToken
        );

        // The seed's contributors may write the tenant row, which issues it new concurrency tokens, so the tenant is read
        // again once the seed has committed and its steps have run; a tenant materialized from it carries the tokens of
        // the row its own seed committed (ADR-1166).
        return await FindTenantAsync(tenantId, cancellationToken)
            ?? throw TenantErrors.NotFound(tenantId);
    }

    /// <summary>
    /// Seeds the specified tenant in one unit of work, retries a commit that another replica's write lost, and runs the
    /// steps queued for after the commit, without reading the committed row.
    /// </summary>
    private async ValueTask SeedInUnitOfWorkAsync(
        string tenantId,
        TenantPlane plane,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; ; ++attempt)
        {
            // Scope the whole seed to this tenant so every seed handler's tenant-bound queries resolve to it.
            using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<ITenantStore>();

            var persistedTenant =
                await store.GetOrDefaultAsync(tenantId, cancellationToken)
                ?? throw TenantErrors.NotFound(tenantId);

            var context = new TenantSeedContext
            {
                Tenant = persistedTenant,
                Plane = plane,
                StoreManager = storeManager,
            };

            // Every seed handler runs and enlists in the shared unit of work; the seeded data commits together below.
            await Mediator.SendAsync(new SeedTenantCommand(context), cancellationToken);

            try
            {
                await storeManager.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException && attempt < MaxSeedAttempts)
            {
                // Another seed of the same tenant — a second replica, or a second request that missed the tenant cache —
                // may have committed between this seed's reads and its write. Seed contributors are idempotent, so
                // seeding again in a fresh unit of work observes what that seed wrote and adds only what is still
                // missing, and the steps queued for after this attempt's commit never run. A failure that persists is
                // genuine and surfaces from the last attempt.
                continue;
            }

            // The seed is committed, so each effect that follows it runs on its own bounded token: the request's token
            // reaches none of them, and a disconnect cannot leave the seed committed without its effects. No caller
            // token reaches a step, so every failure, its timeout included, is logged, and the remaining steps run:
            // the seed has committed, and a tenant whose keys are in the store must materialize.
            foreach (var step in context.PostCommitSteps)
            {
                try
                {
                    await PostCommitWork.RunAsync(
                        TimeProvider,
                        static (step, token) => step(token),
                        step
                    );
                }
                catch (Exception exception)
                {
                    Logger.TenantSeedPostCommitStepFailed(tenantId, exception);
                }
            }

            return;
        }
    }

    private async ValueTask<PersistedTenant?> FindTenantAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();
        return await store.GetOrDefaultAsync(tenantId, cancellationToken);
    }

    private static PersistedTenant CreateEmptyPersistedTenant(
        string teamId,
        string tenantId,
        string displayName,
        string? issuer,
        string? mtlsEndpointAliasAuthority
    )
    {
        var settings = new PersistedTenantSettings
        {
            TenantId = tenantId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = JsonSerializer.SerializeToElement(null, typeof(object)),
        };

        var secrets = new PersistedTenantSecrets
        {
            TenantId = tenantId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            Value = [],
        };

        return new PersistedTenant
        {
            TenantId = tenantId,
            TeamId = teamId,
            // The tenant's request-independent identity; only a tenant its strategy does not serve has none.
            Issuer = issuer,
            MtlsEndpointAliasAuthority = mtlsEndpointAliasAuthority,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            IsDisabled = false,
            DisplayName = displayName,
            Settings = settings,
            Secrets = secrets,
        };
    }
}
