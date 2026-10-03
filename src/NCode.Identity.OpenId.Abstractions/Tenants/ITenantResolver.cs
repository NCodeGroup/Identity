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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Selects the tenant for the current HTTP request by choosing the configured <see cref="ITenantStrategy"/> and
/// delegating to it. This is the low-level tenant-selection seam shared by the OpenID runtime (which materializes a
/// full tenant on top of it) and the management API (which enforces a tenant boundary on top of it).
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
}
