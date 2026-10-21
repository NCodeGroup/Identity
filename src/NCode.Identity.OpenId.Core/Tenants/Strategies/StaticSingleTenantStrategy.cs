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
/// Provides a tenant-selection strategy that always resolves the same single, statically-configured tenant, creating it
/// on first use when it does not yet exist.
/// </summary>
internal class StaticSingleTenantStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor
) : TenantStrategy(storeManagerFactory, optionsAccessor)
{
    private StaticSingleTenantOptions Options =>
        ResolutionOptions.StaticSingle ?? new StaticSingleTenantOptions();

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

    private bool IsConfiguredTenant(string tenantId) =>
        string.Equals(tenantId, ResolveConfiguredTenantId(), StringComparison.Ordinal);

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
    /// <remarks>
    /// The configured tenant's default issuer is its configured issuer. The strategy serves no other tenant, so it
    /// assigns no other tenant an issuer — the root tenant included, unless it is the configured tenant.
    /// </remarks>
    public override ValueTask<string?> GetDefaultIssuerAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var issuer = IsConfiguredTenant(tenantId) ? Options.Issuer : null;
        return ValueTask.FromResult(string.IsNullOrEmpty(issuer) ? null : issuer);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The configured tenant's default alias is its configured alias; the strategy assigns no other tenant one.
    /// </remarks>
    public override ValueTask<string?> GetDefaultMtlsEndpointAliasAuthorityAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var alias = IsConfiguredTenant(tenantId) ? Options.MtlsEndpointAliasAuthority : null;
        return ValueTask.FromResult(string.IsNullOrEmpty(alias) ? null : alias);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The strategy also requires its configured tenant, which it creates on first use.
    /// </remarks>
    public override bool IsRequiredTenant(string tenantId) =>
        base.IsRequiredTenant(tenantId) || IsConfiguredTenant(tenantId);

    /// <inheritdoc />
    /// <remarks>
    /// The strategy serves only the configured tenant, so only it needs an issuer.
    /// </remarks>
    protected override bool RequiresIssuer(string tenantId) => IsConfiguredTenant(tenantId);

    /// <inheritdoc />
    /// <remarks>
    /// The strategy serves only the configured tenant, so no other tenant can issue a token; the configured tenant is
    /// therefore the control-plane tenant.
    /// </remarks>
    public override async ValueTask<PersistedTenant> ResolveControlPlaneTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) => await ResolveTenantAsync(httpContext, cancellationToken);

    /// <inheritdoc />
    public override async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var tenantId = ResolveConfiguredTenantId();

        var displayName = Options.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = StaticSingleTenantOptions.DefaultDisplayName;

        // The configured tenant is created on first resolution, with its configured issuer; the caller that
        // materializes it seeds it (ADR-0166). The provisioner is scoped (its mediator is request-scoped), so it is
        // resolved from the request's service provider.
        var provisioner = httpContext.RequestServices.GetRequiredService<ITenantProvisioner>();
        var persistedTenant = await provisioner.EnsureCreatedAsync(
            tenantId,
            displayName,
            cancellationToken
        );

        return ThrowIfDisabled(persistedTenant);
    }
}
