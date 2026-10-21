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
/// Selects the tenant for the current HTTP request by choosing the configured <see cref="ITenantStrategy"/> and
/// delegating to it. This is the low-level tenant-selection seam shared by the OpenID runtime (which materializes a
/// full tenant on top of it) and the management API (which enforces a tenant boundary on top of it). It also judges
/// whether a proposed tenant can be served, so every place a tenant is written applies the same addressing rules as
/// the requests that later reach it.
/// </summary>
[PublicAPI]
public interface ITenantResolver
{
    /// <summary>
    /// Gets the <c>RoutePattern</c> for the tenant's relative base path. The pattern is empty when tenancy is not
    /// path-based.
    /// </summary>
    /// <returns>The <c>RoutePattern</c> instance for the tenant.</returns>
    RoutePattern GetTenantRoute();

    /// <summary>
    /// Attempts to determine the tenant identifier for the current HTTP request from the request alone, without a
    /// store round-trip. This enables the caller to probe the tenant cache before loading the tenant. When the
    /// configured strategy cannot identify the tenant without the store (for example, dynamic-by-host, which maps a
    /// domain to a tenant), this returns <see langword="false"/>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="tenantId">When this method returns <see langword="true"/>, contains the resolved tenant identifier.</param>
    /// <returns><see langword="true"/> if the tenant identifier was determined without a store round-trip; otherwise,
    /// <see langword="false"/>.</returns>
    bool TryGetTenantId(HttpContext httpContext, [NotNullWhen(true)] out string? tenantId);

    /// <summary>
    /// Resolves the <see cref="PersistedTenant"/> for the current HTTP request.
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
    /// Resolves the control-plane tenant, which authenticates the management API whatever tenant the request addresses.
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
    /// Gets the issuer the configured <see cref="ITenantStrategy"/> assigns a tenant that is written without one — its
    /// configured issuer or the expansion of its issuer template. Every place a tenant is written applies it before
    /// <see cref="ValidateTenantAsync"/>, which judges a derived issuer exactly like an explicit one.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the default issuer, or
    /// <c>null</c> when the identifier is not a valid tenant identifier or the strategy assigns the tenant none.</returns>
    ValueTask<string?> GetDefaultIssuerAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates that a tenant with the specified identifier and issuer can be served: the identifier is 1 to 63
    /// lowercase letters, digits, or hyphens that starts and ends with a letter or digit; the issuer, when present, is
    /// an absolute <c>https</c> URL (<c>http</c> only for a loopback host) with no user information, query, or fragment;
    /// no other tenant holds the issuer, and it is not the root tenant's own address; and the configured
    /// <see cref="ITenantStrategy"/> can route a request to the tenant beneath that issuer, which for every tenant it
    /// serves means the tenant needs one. Call it wherever a tenant's identifier or issuer is written, so a tenant can
    /// never be given an address nothing is served under.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="issuer">The proposed issuer of the tenant, or <c>null</c> or empty when it has none.</param>
    /// <param name="mtlsEndpointAliasAuthority">The proposed mutual-TLS endpoint-alias authority of the tenant (RFC 8705
    /// §5), or <c>null</c> or empty when it has none. When present, it must be an <c>https</c> origin, and the configured
    /// <see cref="ITenantStrategy"/> judges whether a request to it reaches the tenant.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the unit of work the tenant is written in.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the failures; empty
    /// when the tenant can be served.</returns>
    ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        string? issuer,
        string? mtlsEndpointAliasAuthority,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
