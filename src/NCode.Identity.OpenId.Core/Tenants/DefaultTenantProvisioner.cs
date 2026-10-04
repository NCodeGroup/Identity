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
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the default implementation of <see cref="ITenantProvisioner"/> that ensures the root tenant exists before
/// the target tenant, then seeds the target tenant by fanning out <see cref="SeedTenantCommand"/> to every registered
/// handler within a single unit of work.
/// </summary>
internal sealed class DefaultTenantProvisioner(
    IStoreManagerFactory storeManagerFactory,
    IAmbientTenantAccessor ambientTenantAccessor,
    IOptions<TenantResolutionOptions> optionsAccessor,
    IMediator mediator
) : ITenantProvisioner
{
    private const string RootDisplayName = "Root Tenant";

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IMediator Mediator { get; } = mediator;

    private string RootTenantId => optionsAccessor.Value.RootTenantId;

    /// <inheritdoc />
    public async ValueTask<PersistedTenant> ProvisionAsync(
        string tenantId,
        string displayName,
        CancellationToken cancellationToken
    )
    {
        var isRootTenant = string.Equals(tenantId, RootTenantId, StringComparison.Ordinal);

        // The root (control-plane) tenant must exist so that its control-plane data is seeded, even when the target
        // tenant differs from it (ADR-0024). Provision it first, on this same path, so root and workload tenants are
        // provisioned and seeded identically.
        if (!isRootTenant)
        {
            await ProvisionAsync(RootTenantId, RootDisplayName, cancellationToken);
        }

        // Scope the whole provision+seed to this tenant so every seed handler's tenant-bound queries resolve to it.
        using var tenantScope = AmbientTenantAccessor.BeginScope(tenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        var isNewlyProvisioned = persistedTenant is null;
        if (persistedTenant is null)
        {
            persistedTenant = CreateEmptyPersistedTenant(tenantId, displayName);
            await store.AddAsync(persistedTenant, cancellationToken);

            // Commit the tenant row before seeding so seed handlers can attach child rows (resource servers,
            // clients, secrets) to it; those stores resolve the owning tenant from the database, not the tracker.
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        var context = new TenantSeedContext
        {
            Tenant = persistedTenant,
            Plane = isRootTenant ? TenantPlane.Root : TenantPlane.Workload,
            IsNewlyProvisioned = isNewlyProvisioned,
            StoreManager = storeManager,
        };

        // Every seed handler runs and enlists in the shared unit of work; the seeded data commits together below.
        await Mediator.SendAsync(new SeedTenantCommand(context), cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        return persistedTenant;
    }

    private static PersistedTenant CreateEmptyPersistedTenant(string tenantId, string displayName)
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
            DomainName = null,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            IsDisabled = false,
            DisplayName = displayName,
            Settings = settings,
            Secrets = secrets,
        };
    }
}
