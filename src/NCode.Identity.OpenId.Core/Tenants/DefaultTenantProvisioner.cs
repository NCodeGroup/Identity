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

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Results;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the default implementation of <see cref="ITenantProvisioner"/>. It creates a tenant at the address its
/// strategy derives for it, judged by the same rules as every other tenant written, and it seeds a tenant — after the root
/// tenant — by fanning out <see cref="SeedTenantCommand"/> to every registered handler within a single unit of work.
/// </summary>
// See ADR-0045 (one seeding pipeline) and ADR-0166 (a tenant is provisioned through one pipeline under every strategy).
internal sealed class DefaultTenantProvisioner(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    ITenantResolver tenantResolver,
    IOptions<TenantResolutionOptions> optionsAccessor,
    IMediator mediator
) : ITenantProvisioner
{
    // One retry is enough to converge after losing a race to a concurrent seed of the same tenant (see SeedAsync).
    private const int MaxSeedAttempts = 2;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private ITenantResolver TenantResolver { get; } = tenantResolver;
    private IMediator Mediator { get; } = mediator;

    private RootTenantOptions RootOptions => optionsAccessor.Value.Root;

    /// <inheritdoc />
    public async ValueTask<PersistedTenant> EnsureCreatedAsync(
        string tenantId,
        string displayName,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var existing = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (existing is not null)
            return existing;

        // A tenant created on demand is written like any other: at the address its strategy derives for it, judged by
        // the rules every tenant write applies (ADR-0160, ADR-0165).
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
        // in place under every strategy (ADR-0024, ADR-0166). It is seeded on this same path, so root and workload tenants
        // are seeded identically.
        if (!isRootTenant)
        {
            await EnsureCreatedAsync(rootTenantId, RootOptions.DisplayName, cancellationToken);
            await SeedAsync(rootTenantId, cancellationToken);
        }

        var plane = isRootTenant ? TenantPlane.Root : TenantPlane.Workload;

        for (var attempt = 1; ; ++attempt)
        {
            // Scope the whole seed to this tenant so every seed handler's tenant-bound queries resolve to it.
            using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<ITenantStore>();

            var persistedTenant =
                await store.GetOrDefaultAsync(tenantId, cancellationToken)
                ?? throw TypedResults
                    .NotFound()
                    .AsException($"The tenant with identifier '{tenantId}' could not be found.");

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
                return persistedTenant;
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException && attempt < MaxSeedAttempts)
            {
                // Another seed of the same tenant — a second replica, or a second request that missed the tenant cache —
                // may have committed between this seed's reads and its write. Seed contributors are idempotent, so
                // seeding again in a fresh unit of work observes what that seed wrote and adds only what is still
                // missing. A failure that persists is genuine and surfaces from the last attempt.
            }
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
