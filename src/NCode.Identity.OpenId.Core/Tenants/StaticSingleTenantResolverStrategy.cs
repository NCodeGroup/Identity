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
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a tenant-resolution strategy that always resolves the same single, statically-configured tenant,
/// provisioning an empty tenant on first use when one does not yet exist.
/// </summary>
internal class StaticSingleTenantResolverStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor
) : TenantResolverStrategy(storeManagerFactory)
{
    private StaticSingleTenantOptions Options =>
        optionsAccessor.Value.StaticSingle ?? new StaticSingleTenantOptions();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.StaticSingle;

    /// <inheritdoc />
    protected override PathString TenantPath => Options.TenantPath;

    /// <inheritdoc />
    public override async ValueTask<TenantDescriptor> ResolveDescriptorAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var options = Options;

        var tenantId = options.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            tenantId = StaticSingleTenantOptions.DefaultTenantId;

        var persistedTenant = await GetOrCreateTenantAsync(
            tenantId,
            options,
            propertyBag,
            cancellationToken
        );

        return CreateDescriptor(persistedTenant);
    }

    private async ValueTask<PersistedTenant> GetOrCreateTenantAsync(
        string tenantId,
        StaticSingleTenantOptions options,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        if (
            propertyBag.TryGet<PersistedTenant>(out var cached, tenantId)
            && cached?.TenantId == tenantId
        )
            return cached;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (persistedTenant is null)
        {
            persistedTenant = CreateEmptyPersistedTenant(tenantId, options);
            await store.AddAsync(persistedTenant, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        Stash(persistedTenant, propertyBag);
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
