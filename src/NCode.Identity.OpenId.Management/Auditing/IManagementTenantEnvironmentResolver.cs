// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Resolves the effective <c>environment_type</c> of the tenant a management audit event names, so control-plane
/// records carry <c>tenant_environment_type</c> as protocol records do.
/// </summary>
// See ADR-1289.
internal interface IManagementTenantEnvironmentResolver
{
    /// <summary>
    /// Gets the effective environment type of a tenant, or <see langword="null"/> when the tenant does not exist or its
    /// settings cannot be read. A type remembered for the request with
    /// <see cref="ManagementTenantEnvironment.Remember"/> is returned without reading the store.
    /// </summary>
    /// <param name="httpContext">The current request, which memoizes the resolved types.</param>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The tenant's effective environment type, or <see langword="null"/>.</returns>
    ValueTask<string?> GetEnvironmentTypeAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken
    );
}
