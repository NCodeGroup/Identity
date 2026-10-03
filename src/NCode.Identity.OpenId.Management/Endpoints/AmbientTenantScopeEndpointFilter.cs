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
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint filter for tenant-scoped management endpoints that opens the ambient tenant scope tenant-scoped data
/// access reads (ADR-0018), using the <see cref="OpenIdContext"/> that <see cref="OpenIdEnvironmentEndpointFilter"/>
/// already materialized for the request. Cross-tenant endpoints (tenants, servers) omit this filter so their listings
/// are not restricted to a single tenant's rows.
/// </summary>
internal sealed class AmbientTenantScopeEndpointFilter(IAmbientTenantAccessor ambientTenantAccessor)
    : IEndpointFilter
{
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var openIdContext = context.HttpContext.GetOpenIdContext();
        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        return await next(context);
    }
}
