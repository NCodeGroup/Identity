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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a single tenant-selection strategy (for example: static-single, dynamic-by-host, or dynamic-by-path).
/// The <see cref="ITenantResolver"/> selects the active strategy by <see cref="StrategyCode"/> and delegates to it.
/// </summary>
[PublicAPI]
public interface ITenantStrategy
{
    /// <summary>
    /// Gets the <see cref="string"/> <c>Code</c> that identifies this strategy, matched against
    /// <see cref="TenantResolutionOptions.StrategyCode"/>.
    /// </summary>
    string StrategyCode { get; }

    /// <summary>
    /// Gets the <c>RoutePattern</c> for the tenant's relative base path. The pattern is empty when this strategy is
    /// not path-based.
    /// </summary>
    /// <returns>The <c>RoutePattern</c> instance for the tenant.</returns>
    RoutePattern GetTenantRoute();

    /// <summary>
    /// Attempts to determine the tenant identifier for the current HTTP request from the request alone, without a
    /// store round-trip. This enables the caller to probe the tenant cache before loading the tenant. Strategies that
    /// cannot identify the tenant without the store (for example, dynamic-by-host, which maps a domain to a tenant)
    /// return <see langword="false"/>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="tenantId">When this method returns <see langword="true"/>, contains the resolved tenant identifier.</param>
    /// <returns><see langword="true"/> if the tenant identifier was determined without a store round-trip; otherwise,
    /// <see langword="false"/>.</returns>
    bool TryGetTenantId(HttpContext httpContext, [NotNullWhen(true)] out string? tenantId);

    /// <summary>
    /// Resolves the <see cref="PersistedTenant"/> for the current HTTP request using this strategy. When the request
    /// addresses a tenant that configuration defines — the root tenant, or a static strategy's own tenant — and it does
    /// not exist yet, the strategy creates it through <see cref="ITenantProvisioner.EnsureCreatedAsync"/>. A strategy
    /// never seeds a tenant; the caller that materializes it does.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// <see cref="PersistedTenant"/>.</returns>
    ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the control-plane tenant, which authenticates the management API whatever tenant the request addresses:
    /// the root tenant, created through <see cref="ITenantProvisioner.EnsureCreatedAsync"/> when it does not exist yet,
    /// or, for a strategy that serves only its own tenant, that tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the control-plane
    /// <see cref="PersistedTenant"/>.</returns>
    ValueTask<PersistedTenant> ResolveControlPlaneTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the issuer this strategy assigns a tenant that is written without one: the root tenant's configured issuer,
    /// a static strategy's configured issuer for its own tenant, or the expansion of the strategy's issuer template.
    /// The derived issuer is stored with the tenant and judged by <see cref="ValidateTenantAsync"/> like an explicit
    /// one.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant, already validated as a tenant identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the default issuer, or
    /// <c>null</c> when this strategy assigns the tenant none.</returns>
    ValueTask<string?> GetDefaultIssuerAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the rules this strategy places on a tenant's issuer: whether the tenant needs one — every tenant this
    /// strategy serves does, because a tenant is served only beneath its issuer — and that a request routed by this
    /// strategy reaches the tenant beneath that issuer. The <see cref="ITenantResolver"/> has already validated the rules
    /// every strategy shares — the form of the identifier and the issuer, and that no other tenant holds the issuer —
    /// before it calls the selected strategy.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant, already validated as a tenant identifier.</param>
    /// <param name="issuer">The proposed issuer of the tenant, already validated as an absolute <c>https</c> URL
    /// (<c>http</c> only for a loopback host) with no user information, query, or fragment; or <c>null</c> when it has
    /// none.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the unit of work the tenant is written in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the failures; empty
    /// when this strategy can serve the tenant.</returns>
    ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        Uri? issuer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
