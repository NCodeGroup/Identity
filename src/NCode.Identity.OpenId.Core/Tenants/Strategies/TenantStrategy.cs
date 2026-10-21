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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Results;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Provides a common base implementation of the <see cref="ITenantStrategy"/> abstraction, including route-pattern
/// caching, the default issuer a strategy derives from its issuer template, tenant loading with a <c>404</c> for a
/// missing or disabled tenant, and creating the root tenant on demand when a request reaches its address.
/// </summary>
internal abstract class TenantStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor
) : ITenantStrategy
{
    [MemberNotNullWhen(true, nameof(TenantRouteOrNull))]
    private bool TenantRouteHasValue { get; set; }

    private RoutePattern? TenantRouteOrNull { get; set; }

    /// <summary>
    /// Gets the <see cref="IStoreManagerFactory"/> used to create <see cref="IStoreManager"/> instances.
    /// </summary>
    protected IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <summary>
    /// Gets the tenant-resolution options, which every strategy reads its own section from.
    /// </summary>
    protected TenantResolutionOptions ResolutionOptions => optionsAccessor.Value;

    /// <summary>
    /// Gets the options for the root tenant, which every strategy can create on demand.
    /// </summary>
    protected RootTenantOptions RootOptions => ResolutionOptions.Root;

    /// <inheritdoc />
    public abstract string StrategyCode { get; }

    /// <summary>
    /// Gets the relative base path for the tenant.
    /// </summary>
    protected abstract PathString TenantPath { get; }

    /// <summary>
    /// Gets the template a tenant's default issuer is expanded from, or <c>null</c> when the strategy has none. The
    /// default implementation has none.
    /// </summary>
    protected virtual string? IssuerTemplate => null;

    /// <summary>
    /// Gets the template a tenant's default mutual-TLS endpoint-alias authority is expanded from, or <c>null</c> when the
    /// strategy has none. The default implementation has none.
    /// </summary>
    protected virtual string? MtlsEndpointAliasTemplate => null;

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
    /// The control-plane tenant is the root tenant, which the request need not address: under a dynamic strategy the
    /// management API is reached on any host or path, not beneath the tenant that issued the caller's token.
    /// </remarks>
    // See ADR-0191 (the management API authenticates against the control-plane tenant).
    public virtual async ValueTask<PersistedTenant> ResolveControlPlaneTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) => await GetTenantByIdAsync(httpContext, RootOptions.TenantId, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// The root tenant is given its configured issuer when it has one; every tenant otherwise gets the expansion of the
    /// strategy's issuer template, or none when the strategy has no template.
    /// </remarks>
    // See ADR-0165 (every served tenant has an issuer, and its strategy supplies the default).
    public virtual ValueTask<string?> GetDefaultIssuerAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var rootIssuer = RootOptions.Issuer;
        if (IsRootTenant(tenantId) && !string.IsNullOrEmpty(rootIssuer))
            return ValueTask.FromResult<string?>(rootIssuer);

        var issuerTemplate = IssuerTemplate;
        var defaultIssuer = string.IsNullOrEmpty(issuerTemplate)
            ? null
            : TenantAddressRules.ExpandIssuerTemplate(issuerTemplate, tenantId);

        return ValueTask.FromResult(defaultIssuer);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The root tenant is given its configured alias when it has one; every tenant otherwise gets the expansion of the
    /// strategy's alias template, or none when the strategy has no template.
    /// </remarks>
    // See ADR-0195 (a tenant's mTLS endpoint alias is a routed tenant property).
    public virtual ValueTask<string?> GetDefaultMtlsEndpointAliasAuthorityAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var rootAlias = RootOptions.MtlsEndpointAliasAuthority;
        if (IsRootTenant(tenantId) && !string.IsNullOrEmpty(rootAlias))
            return ValueTask.FromResult<string?>(rootAlias);

        var aliasTemplate = MtlsEndpointAliasTemplate;
        var defaultAlias = string.IsNullOrEmpty(aliasTemplate)
            ? null
            : TenantAddressRules.ExpandIssuerTemplate(aliasTemplate, tenantId);

        return ValueTask.FromResult(defaultAlias);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The base rules are that a tenant this strategy serves has an issuer, and that the issuer ends with the tenant route
    /// bound for the tenant, the path its endpoints and its discovery document are served under.
    /// </remarks>
    public virtual ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        Uri? issuer,
        Uri? mtlsEndpointAliasAuthority,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var failure = issuer is null
            ? ValidateIssuerPresence(tenantId)
            : ValidateIssuerRoute(tenantId, issuer);

        IReadOnlyList<TenantValidationFailure> failures = failure is { } value ? [value] : [];
        return ValueTask.FromResult(failures);
    }

    /// <summary>
    /// Determines whether this strategy serves the specified tenant, and therefore whether the tenant needs an issuer: a
    /// tenant is served only beneath its issuer. The default implementation serves every tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns><see langword="true"/> when the tenant needs an issuer; otherwise, <see langword="false"/>.</returns>
    protected virtual bool RequiresIssuer(string tenantId) => true;

    /// <summary>
    /// Validates that a tenant without an issuer is one this strategy does not serve.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns>The failure, or <c>null</c> when the tenant needs no issuer.</returns>
    // See ADR-0165: nothing derives a tenant's issuer from the request, so a tenant without one cannot be served.
    protected TenantValidationFailure? ValidateIssuerPresence(string tenantId)
    {
        if (!RequiresIssuer(tenantId))
            return null;

        var remedy = string.IsNullOrEmpty(IssuerTemplate)
            ? "Supply one, or configure the tenant strategy's issuer template."
            : "Supply one.";

        return new TenantValidationFailure
        {
            Detail =
                $"The tenant requires an issuer: it is served only beneath its issuer. {remedy}",
        };
    }

    /// <summary>
    /// Validates that the specified issuer ends with the tenant route bound for the specified tenant. OpenID Connect
    /// Discovery requires the issuer to be the URL the tenant's discovery document is fetched beneath, and the tenant's
    /// issuer is its base address as-is, so an issuer that does not end with the route names a location nothing is
    /// served under.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="issuer">The proposed issuer of the tenant.</param>
    /// <returns>The failure, or <c>null</c> when the issuer ends with the route.</returns>
    // See ADR-0138 (an issuer is its tenant's base address) and ADR-0160.
    protected TenantValidationFailure? ValidateIssuerRoute(string tenantId, Uri issuer)
    {
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
    /// Determines whether the specified identifier is the root tenant's.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns><see langword="true"/> when it is the root tenant's identifier; otherwise, <see langword="false"/>.</returns>
    protected bool IsRootTenant(string tenantId) =>
        string.Equals(tenantId, RootOptions.TenantId, StringComparison.Ordinal);

    /// <summary>
    /// Creates the root tenant, which configuration defines, when it does not exist yet. A request that reaches the root
    /// tenant's address therefore finds it on a fresh deployment, which is how the control plane is bootstrapped under a
    /// strategy whose tenants are otherwise created through the management API.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the root tenant.</returns>
    // See ADR-0166 (a tenant is provisioned through one pipeline under every strategy).
    protected async ValueTask<PersistedTenant> EnsureRootTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        // The provisioner is scoped (its mediator is request-scoped), so it is resolved from the request's services.
        var provisioner = httpContext.RequestServices.GetRequiredService<ITenantProvisioner>();
        return await provisioner.EnsureCreatedAsync(
            RootOptions.TenantId,
            RootOptions.DisplayName,
            cancellationToken
        );
    }

    /// <summary>
    /// Loads the <see cref="PersistedTenant"/> for the specified identifier, creating the root tenant on demand, and
    /// throwing <c>404</c> when the tenant is missing or disabled.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant.</returns>
    protected async ValueTask<PersistedTenant> GetTenantByIdAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        PersistedTenant? persistedTenant;
        await using (var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken))
        {
            var store = storeManager.GetStore<ITenantStore>();
            persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        }

        if (persistedTenant is null && IsRootTenant(tenantId))
            persistedTenant = await EnsureRootTenantAsync(httpContext, cancellationToken);

        if (persistedTenant is null)
            throw TypedResults
                .NotFound()
                .AsException($"The tenant with identifier '{tenantId}' could not be found.");

        return ThrowIfDisabled(persistedTenant);
    }

    /// <summary>
    /// Throws <c>404</c> when the specified tenant is disabled, so a disabled tenant is indistinguishable from a missing
    /// one.
    /// </summary>
    /// <param name="persistedTenant">The tenant.</param>
    /// <returns>The tenant, when it is enabled.</returns>
    protected static PersistedTenant ThrowIfDisabled(PersistedTenant persistedTenant)
    {
        if (persistedTenant.IsDisabled)
            throw TypedResults
                .NotFound()
                .AsException(
                    $"The tenant with identifier '{persistedTenant.TenantId}' is disabled."
                );

        return persistedTenant;
    }
}
