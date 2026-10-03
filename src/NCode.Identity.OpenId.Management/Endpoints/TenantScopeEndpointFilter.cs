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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The endpoint filter for the tenant-plane management group (<c>/api/tenants/{tenantId}</c>). It opens the ambient
/// tenant scope from the route's <c>tenantId</c> segment, which the persistence-layer query filter reads to confine
/// every tenant-bound read to the addressed tenant — fail-closed, and independent of the request's resolved tenant
/// (ADR-0042). The group's prefix guarantees the segment is present.
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
            return TypedResults.NotFound();
        }

        using var scope = AmbientTenantAccessor.BeginScope(tenantId);
        return await next(context);
    }
}
