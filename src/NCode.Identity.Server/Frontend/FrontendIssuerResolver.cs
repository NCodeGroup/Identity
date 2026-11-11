// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.Server.Frontend;

/// <summary>
/// Resolves the issuer the frontend bootstrap document publishes when <see cref="FrontendBootstrapOptions.Issuer"/> is
/// unset: the issuer of the tenant that serves the surface — the tenant the request identifies (the only tenant under
/// the static single-tenant strategy), and otherwise the root tenant, which serves the surfaces under the dynamic
/// strategies. The issuer is the tenant's stored issuer, or the one its strategy derives before the tenant is
/// provisioned. It is never derived from the serving request, so a forged or proxied <c>Host</c> cannot change the
/// authority the bundle trusts.
/// </summary>
// See ADR-1182.
internal sealed class FrontendIssuerResolver(
    ITenantResolver tenantResolver,
    IOptions<TenantResolutionOptions> tenantResolutionOptionsAccessor,
    IStoreManagerFactory storeManagerFactory
)
{
    private ITenantResolver TenantResolver { get; } = tenantResolver;

    private IOptions<TenantResolutionOptions> TenantResolutionOptionsAccessor { get; } =
        tenantResolutionOptionsAccessor;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <summary>
    /// Resolves the issuer of the tenant that serves the frontend surface for the specified request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the bootstrap request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the issuer, or
    /// <c>null</c> when the tenant has none and its strategy derives none.</returns>
    public async ValueTask<string?> ResolveIssuerAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var tenantId = TenantResolver.TryGetTenantId(httpContext, out var requestTenantId)
            ? requestTenantId
            : TenantResolutionOptionsAccessor.Value.Root.TenantId;

        await using (var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken))
        {
            var store = storeManager.GetStore<ITenantStore>();
            var persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
            if (!string.IsNullOrEmpty(persistedTenant?.Issuer))
                return persistedTenant.Issuer;
        }

        return await TenantResolver.GetDefaultIssuerAsync(tenantId, cancellationToken);
    }
}
