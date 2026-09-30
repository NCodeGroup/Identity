#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Enforces the optional, per-request tenant isolation boundary for the management API. Endpoint families that
/// operate on <em>tenant-scoped</em> resources (clients and any future tenant-bound resource) call this to reject
/// access to resources outside the request's tenant scope. Central-admin families (tenants, servers) do not call it.
/// </summary>
/// <remarks>
/// The boundary mirrors the OpenID runtime's tenant isolation: when the deployment resolves the tenant from the
/// request (dynamic-by-host or dynamic-by-path), a request scoped to one tenant must not reach another tenant's
/// resources. When tenancy is not request-derived (static-single), the surface is unscoped (central admin) and the
/// check is a no-op.
/// </remarks>
[PublicAPI]
public interface ITenantBoundary
{
    /// <summary>
    /// Checks whether a tenant-scoped resource owned by <paramref name="resourceTenantId"/> is reachable within the
    /// current request's tenant scope.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="resourceTenantId">The identifier of the tenant that owns the resource being accessed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing a
    /// <c>404</c> <see cref="ManagementError"/> when the resource lies outside the request's tenant scope; otherwise
    /// <c>null</c> when the resource is in scope or the surface is unscoped (central admin).</returns>
    ValueTask<ManagementError?> CheckAsync(
        HttpContext httpContext,
        string resourceTenantId,
        CancellationToken cancellationToken
    );
}
