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
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Results;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a common base implementation of the <see cref="ITenantStrategy"/> abstraction, including route-pattern
/// caching and tenant loading with a <c>404</c> for a missing or disabled tenant.
/// </summary>
internal abstract class TenantStrategy(IStoreManagerFactory storeManagerFactory) : ITenantStrategy
{
    [MemberNotNullWhen(true, nameof(TenantRouteOrNull))]
    private bool TenantRouteHasValue { get; set; }

    private RoutePattern? TenantRouteOrNull { get; set; }

    /// <summary>
    /// Gets the <see cref="IStoreManagerFactory"/> used to create <see cref="IStoreManager"/> instances.
    /// </summary>
    protected IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    public abstract string StrategyCode { get; }

    /// <summary>
    /// Gets the relative base path for the tenant.
    /// </summary>
    protected abstract PathString TenantPath { get; }

    /// <inheritdoc />
    public virtual bool TryGetTenantId(
        HttpContext httpContext,
        [NotNullWhen(true)] out string? tenantId
    )
    {
        // By default a strategy cannot identify the tenant without a store round-trip.
        tenantId = null;
        return false;
    }

    /// <inheritdoc />
    public virtual RoutePattern GetTenantRoute()
    {
        if (TenantRouteHasValue)
            return TenantRouteOrNull;

        var tenantRoute = RoutePatternFactory.Parse(TenantPath.Value ?? string.Empty);

        TenantRouteOrNull = tenantRoute;
        TenantRouteHasValue = true;

        return tenantRoute;
    }

    /// <inheritdoc />
    public abstract ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Loads the <see cref="PersistedTenant"/> for the specified identifier, throwing <c>404</c> when it is missing
    /// or disabled.
    /// </summary>
    protected async ValueTask<PersistedTenant> GetTenantByIdAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);

        if (persistedTenant is null)
            throw TypedResults
                .NotFound()
                .AsException($"The tenant with identifier '{tenantId}' could not be found.");

        if (persistedTenant.IsDisabled)
            throw TypedResults
                .NotFound()
                .AsException($"The tenant with identifier '{tenantId}' is disabled.");

        return persistedTenant;
    }
}
