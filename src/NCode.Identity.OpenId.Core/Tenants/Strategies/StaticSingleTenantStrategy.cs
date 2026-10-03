#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.ResourceServers;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Provides a tenant-selection strategy that always resolves the same single, statically-configured tenant,
/// provisioning an empty tenant on first use when one does not yet exist.
/// </summary>
internal class StaticSingleTenantStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor,
    ISystemResourceServerSeeder systemResourceServerSeeder
) : TenantStrategy(storeManagerFactory)
{
    private const string RootDisplayName = "Root Tenant";

    private ISystemResourceServerSeeder SystemResourceServerSeeder { get; } =
        systemResourceServerSeeder;

    private StaticSingleTenantOptions Options =>
        optionsAccessor.Value.StaticSingle ?? new StaticSingleTenantOptions();

    private string RootTenantId => optionsAccessor.Value.RootTenantId;

    /// <inheritdoc />
    public override string StrategyCode => OpenIdConstants.TenantStrategyCodes.StaticSingle;

    /// <inheritdoc />
    protected override PathString TenantPath => Options.TenantPath;

    private string ResolveConfiguredTenantId()
    {
        var tenantId = Options.TenantId;
        return string.IsNullOrEmpty(tenantId)
            ? StaticSingleTenantOptions.DefaultTenantId
            : tenantId;
    }

    /// <inheritdoc />
    public override bool TryGetTenantId(
        HttpContext httpContext,
        [NotNullWhen(true)] out string? tenantId
    )
    {
        tenantId = ResolveConfiguredTenantId();
        return true;
    }

    /// <inheritdoc />
    public override async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var options = Options;

        var tenantId = ResolveConfiguredTenantId();

        var displayName = options.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = StaticSingleTenantOptions.DefaultDisplayName;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        // The root (control-plane) tenant must exist so that its control-plane management resource server is seeded,
        // even when the workload tenant differs from the root tenant (ADR-0024).
        if (!string.Equals(tenantId, RootTenantId, StringComparison.Ordinal))
        {
            await EnsureTenantAsync(
                store,
                storeManager,
                RootTenantId,
                RootDisplayName,
                cancellationToken
            );
        }

        return await EnsureTenantAsync(
            store,
            storeManager,
            tenantId,
            displayName,
            cancellationToken
        );
    }

    private async ValueTask<PersistedTenant> EnsureTenantAsync(
        ITenantStore store,
        IStoreManager storeManager,
        string tenantId,
        string displayName,
        CancellationToken cancellationToken
    )
    {
        var persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (persistedTenant is null)
        {
            persistedTenant = CreateEmptyPersistedTenant(tenantId, displayName);
            await store.AddAsync(persistedTenant, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);

            // A freshly-provisioned tenant is self-contained: seed its reserved system resource servers.
            await SystemResourceServerSeeder.SeedAsync(tenantId, cancellationToken);
        }

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
