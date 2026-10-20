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

namespace NCode.Identity.OpenId.Tenants.Strategies;

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

    /// <inheritdoc />
    /// <remarks>
    /// The base rule is that an issuer ends with the tenant route bound for the tenant, the path its endpoints and its
    /// discovery document are served under.
    /// </remarks>
    public virtual ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        Uri? issuer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<TenantValidationFailure> failures = ValidateIssuerRoute(tenantId, issuer)
            is { } failure
            ? [failure]
            : [];
        return ValueTask.FromResult(failures);
    }

    /// <summary>
    /// Validates that the specified issuer ends with the tenant route bound for the specified tenant. OpenID Connect
    /// Discovery requires the issuer to be the URL the tenant's discovery document is fetched beneath, and the tenant's
    /// configured issuer is its base address as-is, so an issuer that does not end with the route names a location
    /// nothing is served under.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="issuer">The proposed issuer of the tenant, or <c>null</c> when it has none.</param>
    /// <returns>The failure, or <c>null</c> when the issuer is absent or ends with the route.</returns>
    // See ADR-0138 (an issuer is its tenant's base address) and ADR-0160.
    protected TenantValidationFailure? ValidateIssuerRoute(string tenantId, Uri? issuer)
    {
        if (issuer is null)
            return null;

        var routePath = GetTenantRoutePath(tenantId);
        if (TenantAddressRules.IssuerEndsWithRoutePath(issuer, routePath))
            return null;

        return new TenantValidationFailure
        {
            Detail =
                $"The issuer '{issuer.OriginalString}' must end with '{routePath}', the path the tenant's endpoints "
                + "and discovery document are served under.",
        };
    }

    /// <summary>
    /// Gets the tenant route bound for the specified tenant: the path, with a leading slash, that the tenant's
    /// endpoints are served under, or empty when the tenant is served at the root. The default implementation returns
    /// the literal tenant path.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns>The bound tenant route.</returns>
    protected virtual string GetTenantRoutePath(string tenantId) =>
        TenantPath.Value ?? string.Empty;

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
