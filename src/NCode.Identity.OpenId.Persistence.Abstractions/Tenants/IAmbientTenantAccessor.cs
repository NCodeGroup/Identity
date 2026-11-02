// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence.Tenants;

/// <summary>
/// Holds the ambient tenant scope for the current request so that tenant-bound data access can be constrained to a
/// single tenant. When a scope is active, the persistence layer only returns rows owned by <see cref="TenantId"/>, so
/// a resource belonging to another tenant is never materialized (defense against cross-tenant data leaks).
/// </summary>
/// <remarks>
/// The scope flows across asynchronous calls (including data-access components created outside the current dependency
/// injection scope), so it is safe to establish once per request and read from anywhere within that request.
/// </remarks>
[PublicAPI]
public interface IAmbientTenantAccessor
{
    /// <summary>
    /// Gets a value indicating whether an ambient tenant scope is currently active. When <c>false</c>, tenant-bound
    /// data access is unscoped (central-admin surfaces).
    /// </summary>
    bool IsScoped { get; }

    /// <summary>
    /// Gets the identifier of the ambient tenant for the current request, or <c>null</c> when no scope is active.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Establishes an ambient tenant scope for the current request. The scope remains active until the returned
    /// <see cref="IDisposable"/> is disposed, which restores the previous scope.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to scope tenant-bound data access to.</param>
    /// <returns>An <see cref="IDisposable"/> that restores the previous scope when disposed.</returns>
    IDisposable BeginScope(string tenantId);
}
