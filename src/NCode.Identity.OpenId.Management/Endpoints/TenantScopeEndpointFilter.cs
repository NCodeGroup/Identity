// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The endpoint filter for the tenant-plane management group (<c>/api/tenants/{tenantId}</c>). It opens the ambient
/// tenant scope from the route's <c>tenantId</c> segment, which the persistence-layer query filter reads to confine
/// every tenant-bound read to the addressed tenant — fail-closed, and independent of the request's resolved tenant.
/// The group's prefix guarantees the segment is present.
/// </summary>
internal sealed class TenantScopeEndpointFilter(IAmbientTenantAccessor ambientTenantAccessor)
    : IEndpointFilter
{
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var routeValues = context.HttpContext.Request.RouteValues;
        if (
            !routeValues.TryGetValue("tenantId", out var raw)
            || raw is not string tenantId
            || string.IsNullOrEmpty(tenantId)
        )
        {
            return ManagementProblems.ResourceNotFound();
        }

        using var scope = AmbientTenantAccessor.BeginScope(tenantId);
        return await next(context);
    }
}
