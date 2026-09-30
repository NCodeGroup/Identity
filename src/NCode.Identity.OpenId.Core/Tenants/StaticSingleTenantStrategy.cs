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

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.ResourceServers;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

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
    private ISystemResourceServerSeeder SystemResourceServerSeeder { get; } =
        systemResourceServerSeeder;

    private StaticSingleTenantOptions Options =>
        optionsAccessor.Value.StaticSingle ?? new StaticSingleTenantOptions();

    /// <inheritdoc />
    public override string StrategyCode => OpenIdConstants.TenantStrategyCodes.StaticSingle;

    /// <inheritdoc />
    protected override PathString TenantPath => Options.TenantPath;

    /// <inheritdoc />
    public override async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var options = Options;

        var tenantId = options.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            tenantId = StaticSingleTenantOptions.DefaultTenantId;

        return await GetOrCreateTenantAsync(tenantId, options, cancellationToken);
    }

    private async ValueTask<PersistedTenant> GetOrCreateTenantAsync(
        string tenantId,
        StaticSingleTenantOptions options,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (persistedTenant is null)
        {
            persistedTenant = CreateEmptyPersistedTenant(tenantId, options);
            await store.AddAsync(persistedTenant, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);

            // A freshly-provisioned tenant is self-contained: seed its reserved system resource servers.
            await SystemResourceServerSeeder.SeedAsync(tenantId, cancellationToken);
        }

        return persistedTenant;
    }

    private static PersistedTenant CreateEmptyPersistedTenant(
        string tenantId,
        StaticSingleTenantOptions options
    )
    {
        var displayName = options.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = StaticSingleTenantOptions.DefaultDisplayName;

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
