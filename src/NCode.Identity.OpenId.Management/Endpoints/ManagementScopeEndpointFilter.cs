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
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The endpoint filter for the management <c>/api</c> surface. It materializes the OpenID request environment (the
/// server and the request's resolved tenant) and publishes the <see cref="OpenIdContext"/> for endpoints and the
/// authorization handler to read, then opens the ambient tenant scope that the persistence-layer query filter reads.
/// Unlike the protocol surface, the ambient scope here is driven by the route's <c>tenantId</c> segment when present
/// (a tenant-bound resource under <c>/api/tenants/{tenantId}/…</c>), so a central-admin caller can administer any
/// tenant's children by id and the scope matches the authorized node (ADR-0042). Control-plane routes carry no
/// <c>tenantId</c> and fall back to the resolved tenant, which is a no-op for the unscoped server/tenant rows they act
/// on.
/// </summary>
internal sealed class ManagementScopeEndpointFilter(
    IOpenIdContextFactory openIdContextFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : IEndpointFilter
{
    private IOpenIdContextFactory OpenIdContextFactory { get; } = openIdContextFactory;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();

        OpenIdContext openIdContext;
        try
        {
            // The factory publishes the context on the request (an IOpenIdContextFeature) for endpoints to retrieve.
            openIdContext = await OpenIdContextFactory.CreateAsync(
                httpContext,
                mediator,
                httpContext.RequestAborted
            );
        }
        catch (HttpResultException exception)
        {
            return exception.HttpResult;
        }

        // The route's tenantId is the authoritative scope for a tenant-bound resource; absent one (control-plane),
        // fall back to the resolved tenant, which is a no-op for the unscoped server/tenant rows those routes act on.
        var scopeTenantId =
            httpContext.Request.RouteValues.TryGetValue("tenantId", out var raw)
            && raw is string routeTenantId
            && !string.IsNullOrEmpty(routeTenantId)
                ? routeTenantId
                : openIdContext.Tenant.TenantId;

        using var scope = AmbientTenantAccessor.BeginScope(scopeTenantId);
        return await next(context);
    }
}
