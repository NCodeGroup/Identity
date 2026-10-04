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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Provides a tenant-selection strategy that always resolves the same single, statically-configured tenant,
/// provisioning an empty tenant on first use when one does not yet exist.
/// </summary>
internal class StaticSingleTenantStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor
) : TenantStrategy(storeManagerFactory)
{
    private StaticSingleTenantOptions Options =>
        optionsAccessor.Value.StaticSingle ?? new StaticSingleTenantOptions();

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

        // Lazily provision and seed on first resolution. The provisioner ensures the root tenant first, then the
        // workload tenant, and runs the seed fan-out for each within a single unit of work (ADR-0045). The mediator
        // is request-scoped, so the provisioner is resolved from the request's service provider.
        var provisioner = httpContext.RequestServices.GetRequiredService<ITenantProvisioner>();

        return await provisioner.ProvisionAsync(tenantId, displayName, cancellationToken);
    }
}
